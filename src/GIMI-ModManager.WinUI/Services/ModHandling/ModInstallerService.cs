using System.Diagnostics.CodeAnalysis;
using CommunityToolkitWrapper;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Entities.Mods.Contract;
using GIMI_ModManager.Core.Entities.Mods.SkinMod;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.Core.Services;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Models.Settings;
using GIMI_ModManager.WinUI.Services.AppManagement;
using GIMI_ModManager.WinUI.ViewModels;
using GIMI_ModManager.WinUI.Views;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Serilog;
using static GIMI_ModManager.WinUI.Views.ModInstallerPage;

namespace GIMI_ModManager.WinUI.Services.ModHandling;

public class ModInstallerService(
    IWindowManagerService windowManagerService,
    ILocalSettingsService localSettingsService)
{
    private readonly ILocalSettingsService _localSettingsService = localSettingsService;
    private readonly IWindowManagerService _windowManagerService = windowManagerService;


    public async Task<InstallMonitor> StartModInstallAsync(DirectoryInfo modFolder,
        ICharacterModList modList, WindowEx? window = null, ICharacterSkin? inGameSkin = null,
        Action<InstallOptions>? setup = null)
    {
        ArgumentNullException.ThrowIfNull(modFolder);
        ArgumentNullException.ThrowIfNull(modList);

        EnsureModObjectIsCharacterIfInGameSkin(modList, inGameSkin);

        var dispatcherQueue = window?.DispatcherQueue ?? App.MainWindow.DispatcherQueue;

        var modOptions = new InstallOptions();
        setup?.Invoke(modOptions);


        var installMonitor = await dispatcherQueue.EnqueueAsync(async () =>
        {
            var (modTitle, installerPage, settings, viewMode) =
                await InitModPageAsync(modFolder, modList, inGameSkin, modOptions, window is null);

            return viewMode switch
            {
                ViewMode.Dialog => await InternalDialogInstallAsync(dispatcherQueue, modTitle,
                    installerPage, window!),
                ViewMode.Window => await InternalWindowInstallAsync(dispatcherQueue, modTitle, modList,
                    installerPage, settings),
                _ => throw new ArgumentOutOfRangeException()
            };
        });

        return installMonitor;
    }


    private async Task<InstallMonitor> InternalWindowInstallAsync(DispatcherQueue dispatcherQueue, string modTitle,
        ICharacterModList modList, ModInstallerPage installerPage, ModInstallerSettings settings)
    {
        var monitor = await dispatcherQueue.EnqueueAsync(() =>
        {
            var modInstallWindow = new WindowEx()
            {
                SystemBackdrop = new MicaBackdrop(),
                Title = $"Mod Installer Helper: {modTitle}",
                Content = installerPage,
                Width = 1200,
                Height = 750,
                MinHeight = 415,
                MinWidth = 1024,
                IsAlwaysOnTop = settings.ModInstallerWindowOnTop
            };
            _windowManagerService.CreateWindow(modInstallWindow, modList);

            var installMonitor = new InstallMonitor(installerPage, modInstallWindow.Close);


            modInstallWindow.Closed += (_, _) => installMonitor.CancelHandler();
            return Task.FromResult(installMonitor);
        });


        return monitor;
    }


    private XamlRoot GetXamlRoot(WindowEx window)
    {
        if (window.Content is Page element)
        {
            return element.XamlRoot;
        }

        if (window.WindowContent is Page page)
        {
            return page.XamlRoot;
        }

        throw new InvalidOperationException("Could not get XamlRoot from window");
    }

    private Task<InstallMonitor> InternalDialogInstallAsync(DispatcherQueue dispatcherQueue, string modTitle,
        ModInstallerPage installerPage, WindowEx window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var contentDialog = new ContentDialog
        {
            Title = $"Mod Installer Helper: {modTitle}",
            Content = installerPage,
            Width = 1200,
            Height = 750,
            MinHeight = 415,
            MinWidth = 1024
        };

        var _ = dispatcherQueue.EnqueueAsync(async () =>
        {
            contentDialog.XamlRoot = GetXamlRoot(window);
            contentDialog.Resources["ContentDialogMaxWidth"] = 8000;
            contentDialog.Resources["ContentDialogMaxHeight"] = 4000;

            contentDialog.SizeChanged += (s, e) =>
            {
                var width = window.Width;
                var height = window.Height;

                if (window.Content is FrameworkElement element)
                {
                    width = element.ActualWidth;
                    height = element.ActualHeight;
                }

                width = 0.8 * width;

                height = 0.8 * height;


                int hOffset = (width < 1300) ? 100 : 300;
                installerPage.MinWidth = width - hOffset;
                installerPage.MinHeight = height - 100;
                installerPage.MaxWidth = width - hOffset;
                installerPage.MaxHeight = height - 100;
            };


            await contentDialog.ShowAsync();
            contentDialog.Content = null;
        });

        var installMonitor = new InstallMonitor(installerPage, contentDialog.Hide);

        contentDialog.Closed += (_, _) => installMonitor.CancelHandler();


        return Task.FromResult(installMonitor);
    }


    private void EnsureModObjectIsCharacterIfInGameSkin(ICharacterModList modList, ICharacterSkin? inGameSkin)
    {
        if (inGameSkin is not null && modList.Character is not ICharacter)
            throw new ArgumentException("The mod list must be a character mod list if inGameSkin is not null");
    }


    private async Task<(string modTitle, ModInstallerPage installerPage, ModInstallerSettings settings,
            ModInstallerPage.ViewMode viewMode)>
        InitModPageAsync(DirectoryInfo modFolder, ICharacterModList modList,
            ICharacterSkin? inGameSkin = null, InstallOptions? options = null, bool isWindowNull = false)
    {
        var modTitle = Guid.TryParse(modFolder.Name, out _)
            ? modFolder.EnumerateDirectories().FirstOrDefault()?.Name
            : modFolder.Name;

        modTitle ??= modFolder.Name;

        var modInstallerSettings =
            await _localSettingsService.ReadOrCreateSettingAsync<ModInstallerSettings>(ModInstallerSettings.Key);

        var viewMode = modInstallerSettings.IsDialogMode && !isWindowNull
            ? ModInstallerPage.ViewMode.Dialog
            : ModInstallerPage.ViewMode.Window;

        var modInstallPage = new ModInstallerPage(modList, modFolder, viewMode, inGameSkin, options);

        return new ValueTuple<string, ModInstallerPage, ModInstallerSettings, ModInstallerPage.ViewMode>(modTitle,
            modInstallPage,
            modInstallerSettings, viewMode);
    }
}

public class InstallOptions
{
    public Uri? ModUrl { get; set; }
}

public sealed class InstallMonitor : IDisposable
{
    private readonly TaskCompletionSource<CloseRequestedArgs> _taskCompletionSource = new();
    private readonly ModInstallerPage _modInstallerPage;
    private readonly Action _closeFunc;
    private CancellationTokenRegistration? _cancellationTokenRegistration;


    public InstallMonitor(ModInstallerPage modInstallerPage, Action closeFunc)
    {
        _modInstallerPage = modInstallerPage;
        _closeFunc = closeFunc;

        _modInstallerPage.CloseRequested += (_, e) =>
        {
            _taskCompletionSource.SetResult(e);
            _closeFunc();
        };
    }

    /// <summary>
    /// Get the cancel handler. This will cancel the task. Should be called when the window is closed or the dialog is closed.
    /// </summary>
    public Action CancelHandler => () =>
    {
        if (!_taskCompletionSource.Task.IsCompleted)
        {
            _taskCompletionSource.TrySetResult(new CloseRequestedArgs(CloseRequestedArgs.CloseReasons.Canceled));
        }
    };

    public Task<CloseRequestedArgs> WaitForCloseAsync(CancellationToken cancellationToken = default)
    {
        _cancellationTokenRegistration = cancellationToken.Register(() =>
        {
            if (!_taskCompletionSource.Task.IsCompleted)
            {
                _taskCompletionSource.TrySetCanceled();
                _closeFunc();
            }
        });

        return _taskCompletionSource.Task;
    }

    public void Dispose()
    {
        _cancellationTokenRegistration?.Dispose();
        _cancellationTokenRegistration = null;
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

        if (ModFolderHelpers.FolderNameEquals(dupeModNewFolderName, options.NewModFolderName!))
            throw new ArgumentException("The new mod folder name and old folder name cannot be the same");

        ReleaseLockedFiles();

        var skinMod = await CreateSkinModWithOptionsAsync(options);
        var newModRenamed = false;
        if (!options.NewModFolderName.IsNullOrEmpty() &&
            !ModFolderHelpers.FolderNameEquals(skinMod.Name, options.NewModFolderName))
        {
            var tmpFolder = App.GetUniqueTmpFolder();
            skinMod = await SkinMod.CreateModAsync(skinMod.CopyTo(tmpFolder.FullName).FullPath).ConfigureAwait(false);
            skinMod.Rename(options.NewModFolderName);
            newModRenamed = true;
        }

        if (!dupeModNewFolderName.IsNullOrEmpty() &&
            !ModFolderHelpers.FolderNameEquals(dupeMod.Name, dupeModNewFolderName))
        {
            _destinationModList.RenameMod(dupeMod, dupeModNewFolderName);
        }

        // Set new custom name for dupe mod
        if (!dupeModNewCustomName.IsNullOrEmpty())
        {
            var dupeModSettings = await dupeMod.Settings.ReadSettingsAsync().ConfigureAwait(false);

            await dupeMod.Settings
                .SaveSettingsAsync(
                    dupeModSettings.DeepCopyWithProperties(customName: NewValue<string?>.Set(dupeModNewCustomName)))
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