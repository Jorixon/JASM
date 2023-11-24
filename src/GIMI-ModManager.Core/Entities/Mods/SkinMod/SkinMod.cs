using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Helpers;

namespace GIMI_ModManager.Core.Entities.Mods.SkinMod;

public class SkinMod : Mod, ISkinMod
{
    private const string ModIniName = "merged.ini";
    private string _modIniPath = string.Empty;
    private readonly string configFileName = Constants.ModConfigFileName;
    private string _configFilePath = string.Empty;

    public Guid Id { get; private set; }

    public SkinModSettingsManager Settings { get; private set; } = null!;
    public SkinModKeySwapManager? KeySwaps { get; private set; }


    public bool HasMergedInI => KeySwaps is not null;


    public void ClearCache()
    {
        Settings.ClearSettings();
        KeySwaps?.ClearKeySwaps();
    }


    private SkinMod(DirectoryInfo modDirectory) : base(modDirectory)
    {
        Init();
    }


    public static Task<ISkinMod> CreateModAsync(string fullPath, bool forceGenerateNewId = false)
    {
        if (!Path.IsPathFullyQualified(fullPath))
            throw new ArgumentException("Path must be absolute.", nameof(fullPath));

        var modDirectory = new DirectoryInfo(fullPath);

        return CreateModAsync(modDirectory, forceGenerateNewId);
    }

    public static async Task<ISkinMod> CreateModAsync(DirectoryInfo modFolder, bool forceGenerateNewId = false)
    {
        if (!modFolder.Exists)
            throw new DirectoryNotFoundException($"Directory not found at path: {modFolder.FullName}");


        var skinMod = new SkinMod(modFolder);
        skinMod.Settings = new SkinModSettingsManager(skinMod);

        if (HasMergedInIFile(modFolder) is not null)
            skinMod.KeySwaps = new SkinModKeySwapManager(skinMod);


        skinMod.Id = await skinMod.Settings.InitializeAsync();

        if (!forceGenerateNewId) return skinMod;

        // TODO: Redo cached settings, as the way it's done now is a bit of a mess.
        // settings was lazy loaded before, but now it's not. This leads to unnecessary complexity and code checks.
        var settings = await skinMod.Settings.ReadSettingsAsync().ConfigureAwait(false);
        settings.Id = Guid.NewGuid();
        skinMod.Id = settings.Id;
        await skinMod.Settings.SaveSettingsAsync(settings).ConfigureAwait(false);


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

    public bool ContainsOnlyJasmFiles()
    {
        return _modDirectory.EnumerateFiles()
            .All(file => file.Name.StartsWith(".JASM_", StringComparison.CurrentCultureIgnoreCase));
    }

    private static string? HasMergedInIFile(DirectoryInfo modDirectory)
    {
        var mergedIniPath = modDirectory.EnumerateFiles("merged.ini", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(iniFiles => iniFiles.Name.Equals(ModIniName, StringComparison.CurrentCultureIgnoreCase))
            ?.FullName;

        if (mergedIniPath is not null)
            return mergedIniPath;

        return modDirectory.EnumerateFiles("*.ini", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(iniFiles => iniFiles.Name.Equals(ModIniName, StringComparison.CurrentCultureIgnoreCase))
            ?.FullName;
    }

    public string? GetModIniPath() => HasMergedInIFile(_modDirectory);

    public string GetNameWithoutDisabledPrefix() =>
        ModFolderHelpers.GetFolderNameWithoutDisabledPrefix(_modDirectory.Name);

    public static bool operator ==(SkinMod? left, SkinMod? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (ReferenceEquals(left, null)) return false;
        if (ReferenceEquals(right, null)) return false;
        return left.Id.Equals(right.Id);
    }

    public static bool operator !=(SkinMod? left, SkinMod? right)
    {
        return !(left == right);
    }

    public bool Equals(ISkinMod? x, ISkinMod? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (ReferenceEquals(x, null)) return false;
        if (ReferenceEquals(y, null)) return false;
        return x.Id.Equals(y.Id);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals(this, (SkinMod)obj);
    }

    public bool Equals(ISkinMod? other)
    {
        return Equals(this, other);
    }

    public override int GetHashCode()
    {
        // ReSharper disable once NonReadonlyMemberInGetHashCode
        return Id.GetHashCode();
    }

    public int GetHashCode(ISkinMod obj)
    {
        return obj.Id.GetHashCode();
    }
}