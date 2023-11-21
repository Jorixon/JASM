using Windows.Storage;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Services;
using GIMI_ModManager.WinUI.Services.AppManagment;
using GIMI_ModManager.WinUI.Services.ModHandling;
using Serilog;
using static GIMI_ModManager.WinUI.Services.ModDragAndDropService.DragAndDropFinishedArgs;

namespace GIMI_ModManager.WinUI.Services;

public class ModDragAndDropService
{
    private readonly ILogger _logger;
    private readonly ModInstallerService _modInstallerService;
    private readonly IWindowManagerService _windowManagerService;


    private readonly NotificationManager _notificationManager;

    public event EventHandler<DragAndDropFinishedArgs>? DragAndDropFinished;

    public ModDragAndDropService(ILogger logger, NotificationManager notificationManager,
        ModInstallerService modInstallerService, IWindowManagerService windowManagerService)
    {
        _notificationManager = notificationManager;
        _modInstallerService = modInstallerService;
        _windowManagerService = windowManagerService;
        _logger = logger.ForContext<ModDragAndDropService>();
    }

    // Drag and drop directly from 7zip is REALLY STRANGE, I don't know why 7zip 'usually' deletes the files before we can copy them
    // Sometimes only a few folders are copied, sometimes only a single file is copied, but usually 7zip removes them and the app just crashes
    // This code is a mess, but it works.
    public async Task AddStorageItemFoldersAsync(
        ICharacterModList modList, IReadOnlyList<IStorageItem>? storageItems)
    {
        if (storageItems is null || !storageItems.Any())
        {
            _logger.Warning("Drag and drop files called with null/0 storage items.");
            return;
        }


        if (storageItems.Count > 1)
        {
            _notificationManager.ShowNotification(
                "Drag and drop called with more than one storage item, this is currently not supported", "",
                TimeSpan.FromSeconds(5));
            return;
        }

        if (_windowManagerService.GetWindow(modList) is not null)
        {
            _notificationManager.ShowNotification(
                $"Please finish adding the mod for '{modList.Character.DisplayName}' first",
                $"JASM does not support multiple mod installs for the same character",
                TimeSpan.FromSeconds(5));
            return;
        }

        // Warning mess below
        foreach (var storageItem in storageItems)
        {
            if (storageItem is StorageFile)
            {
                var scanner = new DragAndDropScanner();
                var extractResult = scanner.ScanAndGetContents(storageItem.Path);


                await _modInstallerService.StartModInstallationAsync(
                    new DirectoryInfo(extractResult.ExtractedFolder.FullPath), modList);

                continue;
            }

            if (storageItem is not StorageFolder sourceFolder)
            {
                _logger.Information("Unknown storage item type from drop: {StorageItemType}", storageItem.GetType());
                continue;
            }

            var destDirectoryInfo = App.GetUniqueTmpFolder();
            destDirectoryInfo.Create();
            destDirectoryInfo = new DirectoryInfo(Path.Combine(destDirectoryInfo.FullName, storageItem.Name));


            _logger.Debug("Source destination folder for drag and drop: {Source}", sourceFolder.Path);
            _logger.Debug("Copying folder {FolderName} to {DestinationFolder}", sourceFolder.Path,
                destDirectoryInfo.FullName);


            var sourceFolderPath = sourceFolder.Path;


            if (sourceFolderPath is null)
            {
                _logger.Warning("Source folder path is null, skipping.");
                continue;
            }

            var tmpFolder = Path.GetTempPath();

            Action<StorageFolder, StorageFolder> recursiveCopy = null!;

            if (sourceFolderPath.Contains(tmpFolder)) // Is 7zip
            {
                destDirectoryInfo = new DirectoryInfo(Path.Combine(destDirectoryInfo.FullName, sourceFolder.Name));
                recursiveCopy = RecursiveCopy7z;
            }
            else
            {
                destDirectoryInfo = new DirectoryInfo(Path.Combine(destDirectoryInfo.FullName, sourceFolder.Name));
                recursiveCopy = RecursiveCopy;
            }

            destDirectoryInfo.Create();

            try
            {
                recursiveCopy.Invoke(sourceFolder,
                    await StorageFolder.GetFolderFromPathAsync(destDirectoryInfo.FullName));
            }
            catch (Exception)
            {
                Directory.Delete(destDirectoryInfo.FullName);
                throw;
            }

            await _modInstallerService.StartModInstallationAsync(destDirectoryInfo.Parent!, modList)
                .ConfigureAwait(false);
        }

        DragAndDropFinished?.Invoke(this, new DragAndDropFinishedArgs(new List<ExtractPaths>()));
    }

    // ReSharper disable once InconsistentNaming
    private void RecursiveCopy7z(StorageFolder sourceFolder, StorageFolder destinationFolder)
    {
        var tmpFolder = Path.GetTempPath();
        var parentDir = new DirectoryInfo(Path.GetDirectoryName(sourceFolder.Path)!);
        parentDir.MoveTo(Path.Combine(tmpFolder, "JASM_TMP", Guid.NewGuid().ToString("N")));

        var modDir = parentDir.EnumerateDirectories().FirstOrDefault();

        if (modDir is null)
        {
            throw new DirectoryNotFoundException("No valid mod folder found in archive. Loose files are ignored");
        }

        RecursiveCopy(StorageFolder.GetFolderFromPathAsync(modDir.FullName).GetAwaiter().GetResult(),
            destinationFolder);
    }

    private void RecursiveCopy(StorageFolder sourceFolder, StorageFolder destinationFolder)
    {
        if (sourceFolder == null || destinationFolder == null)
            throw new ArgumentNullException("Source and destination folders cannot be null.");

        var sourceDir = new DirectoryInfo(sourceFolder.Path);

        // Copy files
        foreach (var file in sourceDir.GetFiles())
        {
            _logger.Debug("Copying file {FileName} to {DestinationFolder}", file.FullName, destinationFolder.Path);
            if (!File.Exists(file.FullName))
            {
                _logger.Warning("File {FileName} does not exist.", file.FullName);
                continue;
            }

            file.CopyTo(Path.Combine(destinationFolder.Path, file.Name), true);
        }
        // Recursively copy subfolders

        foreach (var subFolder in sourceDir.GetDirectories())
        {
            _logger.Debug("Copying subfolder {SubFolderName} to {DestinationFolder}", subFolder.FullName,
                destinationFolder.Path);
            if (!Directory.Exists(subFolder.FullName))
            {
                _logger.Warning("Subfolder {SubFolderName} does not exist.", subFolder.FullName);
                continue;
            }

            var newSubFolder = new DirectoryInfo(Path.Combine(destinationFolder.Path, subFolder.Name));
            newSubFolder.Create();
            RecursiveCopy(StorageFolder.GetFolderFromPathAsync(subFolder.FullName).GetAwaiter().GetResult(),
                StorageFolder.GetFolderFromPathAsync(newSubFolder.FullName).GetAwaiter().GetResult());
        }
    }


    public class DragAndDropFinishedArgs : EventArgs
    {
        public DragAndDropFinishedArgs(IReadOnlyCollection<ExtractPaths> extractResults)
        {
            ExtractResults = extractResults;
        }

        public IReadOnlyCollection<ExtractPaths> ExtractResults { get; }

        public record ExtractPaths
        {
            public ExtractPaths(string sourcePath, string extractedFolderPath)
            {
                SourcePath = sourcePath;
                ExtractedFolderPath = Path.EndsInDirectorySeparator(extractedFolderPath)
                    ? extractedFolderPath
                    : extractedFolderPath + Path.DirectorySeparatorChar;
            }

            public string SourcePath { get; init; }
            public string ExtractedFolderPath { get; init; }

            public void Deconstruct(out string SourcePath, out string ExtractedFolderPath)
            {
                SourcePath = this.SourcePath;
                ExtractedFolderPath = this.ExtractedFolderPath;
            }
        }
    }
}