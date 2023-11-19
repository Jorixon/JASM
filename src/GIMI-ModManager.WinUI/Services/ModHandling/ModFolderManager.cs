using Serilog;

namespace GIMI_ModManager.WinUI.Services.ModHandling;
// Start with installing (and unzipping) the mod to the temp folder, before this process starts

// If a single folder
// Check if the mod already exists in the mods folder
// If yes -> Ask if the user wants to overwrite the mod or rename the mod or this mod

// If multiple folders / files
// show a dedicated window for the user to decide what to do with the files and folders, set mod name and so on

// Then move the mod to the mods folder
public class ModFolderManager
{
    private readonly ILogger _logger;
    private readonly ModDragAndDropService _modDragAndDropService;

    private DirectoryInfo _workFolder;

    public ModFolderManager(ILogger logger, ModDragAndDropService modDragAndDropService)
    {
        _logger = logger;
        _modDragAndDropService = modDragAndDropService;
    }

    public Task StartModInstallationAsync(DirectoryInfo modFolder)
    {
        throw new NotImplementedException();
    }
}