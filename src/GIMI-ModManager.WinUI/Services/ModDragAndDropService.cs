using GIMI_ModManager.Core.Entities;
using GIMI_ModManager.Core.Services;
using Serilog;
using Windows.Storage;
using GIMI_ModManager.Core.Contracts.Entities;

namespace GIMI_ModManager.WinUI.Services;

public class ModDragAndDropService
{
    private readonly ILogger _logger;

    private readonly NotificationManager _notificationManager;

    public ModDragAndDropService(ILogger logger, NotificationManager notificationManager)
    {
        _notificationManager = notificationManager;
        _logger = logger.ForContext<ModDragAndDropService>();
    }

    // Drag and drop directly from 7zip is REALLY STRANGE, I don't know why 7zip 'usually' deletes the files before we can copy them
    // Sometimes only a few folders are copied, sometimes only a single file is copied, but usually 7zip removes them and the app just crashes
    // This code is a mess, but it works.
    public async Task AddStorageItemFoldersAsync(ICharacterModList modList, IReadOnlyList<IStorageItem>? storageItems)
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

        // Warning mess below
        foreach (var storageItem in storageItems)
        {
            var destDirectoryInfo = new DirectoryInfo(modList.AbsModsFolderPath);
            destDirectoryInfo.Create();


            if (storageItem is StorageFile)
            {
                using var scanner = new DragAndDropScanner();
                var extractResult = scanner.Scan(storageItem.Path);
                extractResult.ExtractedMod.MoveTo(destDirectoryInfo.FullName);
                if (extractResult.IgnoredMods.Any())
                    App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                        _notificationManager.ShowNotification(
                            "Multiple folders detected during extraction, first one was extracted",
                            $"Ignored Folders: {string.Join(" | ", extractResult.IgnoredMods)}",
                            TimeSpan.FromSeconds(7)));
                continue;
            }

            if (storageItem is not StorageFolder sourceFolder)
            {
                _logger.Information("Unknown storage item type from drop: {StorageItemType}", storageItem.GetType());
                continue;
            }


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
                recursiveCopy = RecursiveCopy7z;
            else // StorageFolder from explorer
            {
                destDirectoryInfo = new DirectoryInfo(Path.Combine(modList.AbsModsFolderPath, sourceFolder.Name));
                destDirectoryInfo.Create();
                recursiveCopy = RecursiveCopy;
            }

            //IsAddingModFolder = true; // This was used to disable the UI while adding a mod, but it's not in use anymore

            recursiveCopy.Invoke(sourceFolder,
                await StorageFolder.GetFolderFromPathAsync(destDirectoryInfo.FullName));
        }
    }

    // ReSharper disable once InconsistentNaming
    private static void RecursiveCopy7z(StorageFolder sourceFolder, StorageFolder destinationFolder)
    {
        var tmpFolder = Path.GetTempPath();
        var parentDir = new DirectoryInfo(Path.GetDirectoryName(sourceFolder.Path)!);
        parentDir.MoveTo(Path.Combine(tmpFolder, "JASM_TMP", Guid.NewGuid().ToString("N")));
        var mod = new Mod(parentDir.GetDirectories().First()!);
        mod.MoveTo(destinationFolder.Path);
    }

    private void RecursiveCopy(StorageFolder sourceFolder, StorageFolder destinationFolder)
    {
        if (sourceFolder == null || destinationFolder == null)
        {
            throw new ArgumentNullException("Source and destination folders cannot be null.");
        }

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
}