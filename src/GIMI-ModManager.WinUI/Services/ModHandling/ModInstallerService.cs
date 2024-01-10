using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities.Mods.Contract;
using GIMI_ModManager.Core.Entities.Mods.SkinMod;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.Core.Services;
using GIMI_ModManager.WinUI.Helpers;
using GIMI_ModManager.WinUI.Services.AppManagement;
using GIMI_ModManager.WinUI.Views;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Serilog;

namespace GIMI_ModManager.WinUI.Services.ModHandling;

public class ModInstallerService
{
    private readonly IWindowManagerService _windowManagerService;

    private readonly ConcurrentDictionary<ICharacterModList, WindowEx> _modInstallWindows = new();


    public ModInstallerService(IWindowManagerService windowManagerService)
    {
        _windowManagerService = windowManagerService;
    }

    public async Task<InstallProgressMonitor?> StartModInstallationAsync(DirectoryInfo modFolder,
        ICharacterModList modList,
        ICharacterSkin? inGameSkin = null)
    {
        ArgumentNullException.ThrowIfNull(modFolder);
        ArgumentNullException.ThrowIfNull(modList);


        if (inGameSkin is not null && modList.Character is not ICharacter)
            throw new ArgumentException("The mod list must be a character mod list if inGameSkin is not null");

        var dispatcherQueue = DispatcherQueue.GetForCurrentThread() ?? App.MainWindow.DispatcherQueue;

        var progressMonitorTask = new TaskCompletionSource<InstallProgressMonitor?>();
        dispatcherQueue.TryEnqueue(() =>
        {
            var monitor = InternalStart(modFolder, modList, inGameSkin);
            progressMonitorTask.SetResult(monitor);
        });
        return await progressMonitorTask.Task.ConfigureAwait(false);
    }

    private InstallProgressMonitor? InternalStart(DirectoryInfo modFolder, ICharacterModList modList,
        ICharacterSkin? inGameSkin)
    {
        var modTitle = Guid.TryParse(modFolder.Name, out _)
            ? modFolder.EnumerateDirectories().FirstOrDefault()?.Name
            : modFolder.Name;

        modTitle ??= modFolder.Name;

        var modInstallPage = new ModInstallerPage(modList, modFolder, inGameSkin);
        var modInstallWindow = new WindowEx()
        {
            SystemBackdrop = new MicaBackdrop(),
            Title = $"Mod Installer Helper: {modTitle}",
            Content = modInstallPage,
            Width = 1200,
            Height = 750
        };
        if (!_modInstallWindows.TryAdd(modList, modInstallWindow))
        {
            var window = _modInstallWindows.FirstOrDefault(e => e.Key == modList).Value;
            Win32.PlaySound("SystemAsterisk", nuint.Zero,
                (uint)(Win32.SoundFlags.SND_ALIAS | Win32.SoundFlags.SND_NODEFAULT));
            App.MainWindow.DispatcherQueue.TryEnqueue(() => window?.Activate());
            return null;
        }

        modInstallPage.CloseRequested += (_, _) => { modInstallWindow.Close(); };
        modInstallWindow.Closed += (_, _) => { _modInstallWindows.TryRemove(modList, out _); };
        _windowManagerService.CreateWindow(modInstallWindow, modList);

        return new InstallProgressMonitor(modInstallWindow, modList);
    }


    /// <summary>
    /// This class is returned when an installation starts, it can be used to monitor the progress
    /// of the installation from different parts of the application
    /// </summary>
    public class InstallProgressMonitor
    {
        private WindowEx? _installWindow;
        private readonly ICharacterModList _modList;
        private bool _initialized;

        public bool InstallWindowOpen => _installWindow is not null;
        public IModdableObject ModdableObject => _modList.Character;

        public bool IsFinished { get; private set; }


        public bool Installed { get; private set; }
        public bool IsCanceled { get; private set; }
        public bool IsFailed { get; private set; }

        public event EventHandler<FinishedEventArgs>? Finished;

        public InstallProgressMonitor(WindowEx installWindow, ICharacterModList modList)
        {
            _installWindow = installWindow;
            _modList = modList;

            _installWindow.Closed += (_, _) =>
            {
                _installWindow = null;
                if (!IsFinished)
                {
                    IsCanceled = true;
                    Finished?.Invoke(this, new FinishedEventArgs(false, true, false));
                }
            };
        }


        public void Initialize()
        {
            if (_initialized)
                throw new InvalidOperationException("The progress monitor has already been initialized");

            _initialized = true;
        }

        public void SuccessFullInstall()
        {
            if (!_initialized)
                throw new InvalidOperationException("The progress monitor has not been initialized");

            Installed = true;
            IsFinished = true;

            Finished?.Invoke(this, new FinishedEventArgs(true, true, false));
        }

        public void FailedInstall()
        {
            if (!_initialized)
                throw new InvalidOperationException("The progress monitor has not been initialized");

            IsFailed = true;
            IsFinished = true;

            Finished?.Invoke(this, new FinishedEventArgs(false, true, true));
        }


        public class FinishedEventArgs : EventArgs
        {
            public bool Installed { get; }
            public bool IsFinished { get; }
            public bool IsFailed { get; }


            public FinishedEventArgs(bool installed, bool isFinished, bool isFailed)
            {
                Installed = installed;
                IsFinished = isFinished;
                IsFailed = isFailed;
            }
        }
    }
}

public sealed class ModInstallation : IDisposable
{
    private readonly ModCrawlerService _modCrawlerService = App.GetService<ModCrawlerService>();
    private readonly ISkinManagerService _skinManagerService = App.GetService<ISkinManagerService>();
    private readonly ICharacterModList _destinationModList;
    private readonly DirectoryInfo _originalModFolder;
    private readonly List<FileStream> _lockedFiles = new();

    private FileInfo? _jasmConfigFile;
    public DirectoryInfo ModFolder { get; private set; }
    private DirectoryInfo? _shaderFixesFolder;

    // TODO: Enable later
    private List<FileInfo> _shaderFixesFiles = new();


    private ModInstallation(DirectoryInfo originalModFolder, ICharacterModList destinationModList)
    {
        _originalModFolder = originalModFolder;
        _destinationModList = destinationModList;
        SetRootModFolder(originalModFolder);
        LockFiles();
    }

    private void LockFiles()
    {
        foreach (var fileInfo in _originalModFolder.GetFiles("*", SearchOption.AllDirectories))
        {
            var fileStream = fileInfo.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            _lockedFiles.Add(fileStream);
        }
    }

    // Lock mod folder
    public static ModInstallation Start(DirectoryInfo modFolder, ICharacterModList destinationModList)
    {
        return new ModInstallation(modFolder, destinationModList);
    }

    [MemberNotNull(nameof(ModFolder))]
    public void SetRootModFolder(DirectoryInfo newRootFolder)
    {
        if (newRootFolder.FullName == _shaderFixesFolder?.FullName)
            throw new ArgumentException("The new root folder is the same as the current shader fixes folder");

        if (!newRootFolder.Exists)
            throw new DirectoryNotFoundException($"The folder {newRootFolder.FullName} does not exist");

        ModFolder = new DirectoryInfo(newRootFolder.FullName);
        _jasmConfigFile = _modCrawlerService.GetFirstJasmConfigFileAsync(ModFolder, false);
    }

    public void SetShaderFixesFolder(DirectoryInfo shaderFixesFolder)
    {
        // TODO: Enable later
        return;

        if (shaderFixesFolder.FullName == ModFolder.FullName)
            throw new ArgumentException("The new shader fixes folder is the same as the current root folder");

        if (!shaderFixesFolder.Exists)
            throw new DirectoryNotFoundException($"The folder {shaderFixesFolder.FullName} does not exist");

        _shaderFixesFiles.Clear();
        _shaderFixesFiles.AddRange(shaderFixesFolder.GetFiles("*.txt", SearchOption.TopDirectoryOnly));
        _shaderFixesFolder = shaderFixesFolder;
    }


    public DirectoryInfo? AutoSetModRootFolder()
    {
        var jasmConfigFile = _modCrawlerService.GetFirstJasmConfigFileAsync(_originalModFolder);
        DirectoryInfo? modRootFolder = null;
        if (jasmConfigFile is not null)
        {
            modRootFolder = new DirectoryInfo(jasmConfigFile.DirectoryName!);
        }
        else
        {
            var mergedIniFile = _modCrawlerService.GetMergedIniFile(_originalModFolder);
            if (mergedIniFile is not null)
                modRootFolder = new DirectoryInfo(mergedIniFile.DirectoryName!);
        }

        modRootFolder ??= _originalModFolder.EnumerateDirectories().FirstOrDefault();


        if (modRootFolder is null)
            return null;

        SetRootModFolder(modRootFolder);
        return modRootFolder;
    }

    public DirectoryInfo? AutoSetShaderFixesFolder()
    {
        var shaderFixesFolder = _modCrawlerService.GetShaderFixesFolder(_originalModFolder);
        if (shaderFixesFolder is null)
            return null;

        SetShaderFixesFolder(shaderFixesFolder);
        return shaderFixesFolder;
    }

    public async Task<ModSettings?> TryReadModSettingsAsync()
    {
        if (_jasmConfigFile is null)
            return null;

        await RemoveJasmConfigFileLockAsync().ConfigureAwait(false);
        try
        {
            return await SkinModSettingsManager.ReadSettingsAsync(_jasmConfigFile.FullName);
        }
        catch (Exception)
        {
            // ignored
        }

        LockJasmConfigFile();
        return null;
    }

    public ISkinMod? AnyDuplicateName()
    {
        var skinEntries = _destinationModList.Mods;


        foreach (var skinEntry in skinEntries)
        {
            if (ModFolderHelpers.FolderNameEquals(skinEntry.Mod.Name, ModFolder.Name))
                return skinEntry.Mod;
        }

        return null;
    }

    public async Task<ISkinMod> RenameAndAddAsync(AddModOptions options, ISkinMod dupeMod,
        string dupeModNewFolderName, string? dupeModNewCustomName = null)
    {
        if (dupeModNewFolderName.IsNullOrEmpty() && options.NewModFolderName.IsNullOrEmpty())
            throw new ArgumentException("The new mod folder name and old folder name cannot be null or empty");

        if (dupeModNewFolderName == options.NewModFolderName)
            throw new ArgumentException("The new mod folder name and old folder name cannot be the same");

        ReleaseLockedFiles();

        var skinMod = await CreateSkinModWithOptionsAsync(options);
        var newModRenamed = false;
        if (!options.NewModFolderName.IsNullOrEmpty() && skinMod.Name != options.NewModFolderName)
        {
            var tmpFolder = App.GetUniqueTmpFolder();
            skinMod = await SkinMod.CreateModAsync(skinMod.CopyTo(tmpFolder.FullName).FullPath).ConfigureAwait(false);
            skinMod.Rename(options.NewModFolderName);
            newModRenamed = true;
        }

        if (!dupeModNewFolderName.IsNullOrEmpty() && dupeMod.Name != dupeModNewFolderName)
        {
            _destinationModList.RenameMod(dupeMod, dupeModNewFolderName);
        }

        // Set new custom name for dupe mod
        if (!dupeModNewCustomName.IsNullOrEmpty())
        {
            var dupeModSettings = await dupeMod.Settings.ReadSettingsAsync().ConfigureAwait(false);

            await dupeMod.Settings
                .SaveSettingsAsync(dupeModSettings.DeepCopyWithProperties(customName: dupeModNewCustomName))
                .ConfigureAwait(false);
        }

        _skinManagerService.AddMod(skinMod, _destinationModList, newModRenamed);
        return skinMod;
    }


    public async Task<ISkinMod> AddAndReplaceAsync(ISkinMod dupeMod, AddModOptions? options = null)
    {
        ReleaseLockedFiles();
        var skinMod = await CreateSkinModWithOptionsAsync(options).ConfigureAwait(false);
        try
        {
            _destinationModList.DeleteModBySkinEntryId(dupeMod.Id);
        }
        catch (DirectoryNotFoundException)
        {
        }

        return _skinManagerService.AddMod(skinMod, _destinationModList);
    }

    public async Task<ISkinMod> AddModAsync(AddModOptions? options = null)
    {
        if (AnyDuplicateName() is not null)
            throw new InvalidOperationException("There is already a mod with the same name");

        ReleaseLockedFiles();
        var skinMod = await CreateSkinModWithOptionsAsync(options);

        return _skinManagerService.AddMod(skinMod, _destinationModList);
    }

    private async Task<ISkinMod> CreateSkinModWithOptionsAsync(AddModOptions? options = null)
    {
        await RemoveJasmConfigFileLockAsync().ConfigureAwait(false);

        var skinMod = await SkinMod.CreateModAsync(ModFolder, true).ConfigureAwait(false);

        if (options is null)
            return skinMod;

        var settings = new ModSettings(
            id: skinMod.Id,
            customName: options.ModName,
            imagePath: options.ModImage,
            author: options.Author,
            modUrl: Uri.TryCreate(options.ModUrl, UriKind.Absolute, out var modUrl) ? modUrl : null,
            description: options.Description,
            dateAdded: DateTime.Now
        );
        await skinMod.Settings.SaveSettingsAsync(settings, new SaveSettingsOptions { DeleteOldImage = false })
            .ConfigureAwait(false);
        return skinMod;
    }

    private async Task RemoveJasmConfigFileLockAsync()
    {
        if (_jasmConfigFile is not null)
        {
            _jasmConfigFile.Refresh();
            var jasmFs = _lockedFiles.FirstOrDefault(file =>
                file.Name.Equals(_jasmConfigFile.FullName, StringComparison.OrdinalIgnoreCase));

            if (jasmFs is not null)
            {
                await jasmFs.DisposeAsync().ConfigureAwait(false);
                _lockedFiles.Remove(jasmFs);
            }
        }
    }

    private void LockJasmConfigFile()
    {
        if (_jasmConfigFile is not null)
        {
            _jasmConfigFile.Refresh();
            if (!_jasmConfigFile.Exists) return;

            var jasmFs = _jasmConfigFile.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            _lockedFiles.Add(jasmFs);
        }
    }

    private void ReleaseLockedFiles()
    {
        foreach (var fileStream in _lockedFiles.ToArray())
        {
            fileStream.Dispose();
            _lockedFiles.Remove(fileStream);
        }

        Log.Debug("Released locked files, {time}", DateTime.Now);
    }

    public void Dispose()
    {
        ReleaseLockedFiles();
    }
}

public record AddModOptions
{
    public string? NewModFolderName { get; set; }
    public string? ModName { get; set; }
    public Uri? ModImage { get; set; }
    public string? ModUrl { get; set; }
    public string? Author { get; set; }
    public string? Description { get; set; }
}