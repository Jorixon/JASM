using GIMI_ModManager.Core.Contracts.Entities;

namespace GIMI_ModManager.Core.Entities.Mods.SkinMod;

public class SkinMod : Mod, ISkinMod
{
    private const string ModIniName = "merged.ini";
    private string _modIniPath = string.Empty;
    private const string configFileName = ".JASM_ModConfig.json";
    private string _configFilePath = string.Empty;

    public Guid Id { get; private set; }

    public SkinModSettingsManager Settings { get; private set; } = null!;
    public SkinModKeySwapManager? KeySwaps { get; private set; }
    public bool HasMergedInI { get; private set; }


    public void ClearCache()
    {
        Settings.ClearSettings();
        KeySwaps?.ClearKeySwaps();
    }


    private SkinMod(DirectoryInfo modDirectory) : base(modDirectory)
    {
        Init();
    }


    public static Task<ISkinMod> CreateModAsync(string fullPath)
    {
        if (!Path.IsPathFullyQualified(fullPath))
            throw new ArgumentException("Path must be absolute.", nameof(fullPath));

        var modDirectory = new DirectoryInfo(fullPath);

        return CreateModAsync(modDirectory);
    }

    public static async Task<ISkinMod> CreateModAsync(DirectoryInfo modFolder)
    {
        if (!modFolder.Exists)
            throw new DirectoryNotFoundException($"Directory not found at path: {modFolder.FullName}");


        var skinMod = new SkinMod(modFolder);
        skinMod.Settings = new SkinModSettingsManager(skinMod);

        if (HasMergedInIFile(modFolder) is { } merged)
            skinMod.KeySwaps = new SkinModKeySwapManager(skinMod, merged);

        // TODO: Error handling
        skinMod.Id = await skinMod.Settings.InitializeAsync();


        return skinMod;
    }

    private void Init()
    {
        var modFolderAttributes = File.GetAttributes(_modDirectory.FullName);
        if (!modFolderAttributes.HasFlag(FileAttributes.Directory))
            throw new ArgumentException("Mod must be a folder.", nameof(_modDirectory.FullName));
        Refresh();
    }

    private void Refresh()
    {
        _modDirectory.Refresh();

        _configFilePath = Path.Combine(FullPath, configFileName);
        _modIniPath = Path.Combine(FullPath, ModIniName);
    }

    private static string? HasMergedInIFile(DirectoryInfo modDirectory)
    {
        return modDirectory.EnumerateFiles("*.ini", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(iniFiles => iniFiles.Name.Equals(ModIniName, StringComparison.CurrentCultureIgnoreCase))
            ?.FullName;
    }

    public bool Equals(ISkinMod? x, ISkinMod? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (ReferenceEquals(x, null)) return false;
        if (ReferenceEquals(y, null)) return false;
        return string.Equals(x.FullPath, y.FullPath, StringComparison.CurrentCultureIgnoreCase);
    }

    public int GetHashCode(ISkinMod obj)
    {
        return StringComparer.CurrentCultureIgnoreCase.GetHashCode(obj.FullPath);
    }
}