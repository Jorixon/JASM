using System.Text.Json;
using FuzzySharp.Utils;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Helpers;
using SharpCompress.IO;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace GIMI_ModManager.Core.Entities;

public class SkinMod : Mod, ISkinMod
{
    private const string ImageName = ".JASM_Cover";
    private const string ModIniName = "merged.ini";
    private string _modIniPath = string.Empty;
    private const string configFileName = ".JASM_ModConfig.json";
    private string _configFilePath = string.Empty;
    private readonly List<string> _imagePaths = new();

    private List<SkinModKeySwap>? _keySwaps = new();

    public IReadOnlyCollection<string> ImagePaths => _imagePaths.AsReadOnly();
    public SkinModSettings? CachedSkinModSettings { get; private set; } = null;
    public IReadOnlyCollection<SkinModKeySwap>? CachedKeySwaps => _keySwaps?.AsReadOnly();
    public bool HasMergedInI { get; private set; }


    public SkinMod(IMod mod) : base(new DirectoryInfo(mod.FullPath), mod.CustomName)
    {
        Init();
    }

    public SkinMod(string modPath, string customName = "") : base(new DirectoryInfo(Path.GetFullPath(modPath)),
        customName)
    {
        Init();
    }

    public SkinMod(DirectoryInfo modDirectory, string customName = "") : base(modDirectory, customName)
    {
        Init();
    }

    private void Init()
    {
        var modFolderAttributes = File.GetAttributes(_modDirectory.FullName);
        if (!modFolderAttributes.HasFlag(FileAttributes.Directory))
            throw new ArgumentException("Mod must be a folder.", nameof(_modDirectory.FullName));
        _imagePaths.Add(".JASM_Cover");
        Refresh();
    }

    public void Refresh()
    {
        _modDirectory.Refresh();

        _configFilePath = Path.Combine(FullPath, configFileName);
        _modIniPath = Path.Combine(FullPath, ModIniName);

        HasMergedInI = HasMergedInIFile();
    }

    private bool HasMergedInIFile() =>
        _modDirectory.EnumerateFiles("*.ini", SearchOption.TopDirectoryOnly)
            .Any(iniFiles => iniFiles.Name.Equals(ModIniName, StringComparison.CurrentCultureIgnoreCase));

    public bool IsValidFolder() => Exists() && !IsEmpty();

    public void ClearCache()
    {
        CachedSkinModSettings = null;
        _keySwaps = null;
    }

    public async Task<IReadOnlyCollection<SkinModKeySwap>> ReadKeySwapConfiguration(bool forceReload = false,
        CancellationToken cancellationToken = default)
    {
        Refresh();
        if (!HasMergedInI)
            throw new InvalidOperationException("Mod has no merged.ini file.");

        List<string> keySwapLines = new();
        List<SkinModKeySwap> keySwaps = new();
        var keySwapBlockStarted = false;
        await foreach (var line in File.ReadLinesAsync(_modIniPath, cancellationToken))
        {
            if (IniConfigHelpers.IsSection(line, "[KeySwap]") && keySwapBlockStarted ||
                keySwapBlockStarted && keySwapLines.Count > 9)
            {
                keySwapBlockStarted = false;
                var keySwap = IniConfigHelpers.ParseKeySwap(keySwapLines);
                if (keySwap is not null)
                    keySwaps.Add(keySwap);
                keySwapLines.Clear();
                continue;
            }

            if (IniConfigHelpers.IsSection(line, "[KeySwap]"))
            {
                keySwapBlockStarted = true;
                continue;
            }

            if (keySwapLines.Count > 10 && !line.StartsWith("[KeySwap]", StringComparison.CurrentCultureIgnoreCase) &&
                keySwapBlockStarted)
            {
                keySwapBlockStarted = false;
                keySwapLines.Clear();
                continue;
            }

            if (keySwapBlockStarted)
                keySwapLines.Add(line);
        }

        if (keySwaps.Count == 0)
            return new List<SkinModKeySwap>().AsReadOnly();

        if (_keySwaps is null)
            _keySwaps = new List<SkinModKeySwap>();
        else
            _keySwaps.Clear();


        _keySwaps.AddRange(keySwaps);
        return _keySwaps.AsReadOnly();
    }

    // I wonder how long this abomination will stay in the codebase :)
    public async Task SaveKeySwapConfiguration(ICollection<SkinModKeySwap> updatedKeySwaps,
        CancellationToken cancellationToken = default)
    {
        Refresh();
        if (!HasMergedInI)
            throw new InvalidOperationException("Mod has no merged.ini file.");

        if (updatedKeySwaps.Count == 0)
            throw new ArgumentException("No key swaps to save.", nameof(updatedKeySwaps));

        if (updatedKeySwaps.Count != _keySwaps.Count)
            throw new ArgumentException("Key swap count mismatch.", nameof(updatedKeySwaps));


        // Convoluted way to lock the file for reading and writing.
        var fileLines = new List<string>();

        await using var fileStream = new FileStream(_modIniPath, FileMode.Open, FileAccess.Read, FileShare.None);
        using (var reader = new StreamReader(fileStream))
        {
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
                fileLines.Add(line);
        }

        var sectionStartIndexes = new List<int>();
        for (var i = 0; i < fileLines.Count; i++)
        {
            if (IniConfigHelpers.IsSection(fileLines[i], SkinModKeySwap.KeySwapIniSection))
                sectionStartIndexes.Add(i);
        }

        if (sectionStartIndexes.Count != updatedKeySwaps.Count)
            throw new InvalidOperationException("Key swap count mismatch.");

        if (sectionStartIndexes.Count == 0)
            throw new InvalidOperationException("No key swaps found.");

        for (var i = 0; i < sectionStartIndexes.Count; i++)
        {
            var keySwap = updatedKeySwaps.ElementAt(i);
            var sectionStartIndex = sectionStartIndexes[i] + 1;


            for (var j = sectionStartIndex; j < sectionStartIndex + 8; j++)
            {
                var line = fileLines[j];

                if (IniConfigHelpers.IsIniKey(line, SkinModKeySwap.ForwardIniKey))
                {
                    var value = IniConfigHelpers.FormatIniKey(SkinModKeySwap.ForwardIniKey, keySwap.ForwardHotkey);
                    if (value is null)
                        continue;
                    fileLines[j] = value;
                }

                else if (IniConfigHelpers.IsIniKey(line, SkinModKeySwap.BackwardIniKey))
                {
                    var value = IniConfigHelpers.FormatIniKey(SkinModKeySwap.BackwardIniKey, keySwap.BackwardHotkey);
                    if (value is null)
                        continue;
                    fileLines[j] = value;
                }

                else if (IniConfigHelpers.IsIniKey(line, SkinModKeySwap.TypeIniKey))
                {
                    var value = IniConfigHelpers.FormatIniKey(SkinModKeySwap.TypeIniKey, keySwap.Type);
                    if (value is null)
                        continue;
                    fileLines[j] = value;
                }
                else if (IniConfigHelpers.IsIniKey(line, SkinModKeySwap.SwapVarIniKey))
                {
                    var value = IniConfigHelpers.FormatIniKey(SkinModKeySwap.SwapVarIniKey,
                        string.Join(",", keySwap.SwapVar ?? new string[] { "" }));
                    if (value is null)
                        continue;
                    fileLines[j] = value;
                }

                else if (IniConfigHelpers.IsIniKey(line, SkinModKeySwap.ConditionIniKey))
                {
                    var value = IniConfigHelpers.FormatIniKey(SkinModKeySwap.ConditionIniKey, keySwap.Condition);
                    if (value is null)
                        continue;
                    fileLines[j] = value;
                }

                else if (IniConfigHelpers.IsSection(line))
                    break;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        await using var writeStream = new FileStream(_modIniPath, FileMode.Truncate, FileAccess.Write, FileShare.None);

        await using (var writer = new StreamWriter(writeStream))
        {
            foreach (var line in fileLines)
                await writer.WriteLineAsync(line);
        }

        await ReadKeySwapConfiguration(true, CancellationToken.None);
    }


    public async Task<SkinModSettings> ReadSkinModSettings(bool forceReload = false,
        CancellationToken cancellationToken = default)
    {
        Refresh();

        if (CachedSkinModSettings is not null && !forceReload)
            return CachedSkinModSettings;

        if (!File.Exists(_configFilePath))
            return new SkinModSettings();

        var fileContents = await File.ReadAllTextAsync(_configFilePath, cancellationToken);
        var options = new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        var skinModSettings =
            JsonSerializer.Deserialize<SkinModSettings>(fileContents, options) ?? new SkinModSettings();

        SetAbsoluteImagePath(skinModSettings);

        CachedSkinModSettings = skinModSettings;

        return skinModSettings;
    }


    private Task CopyAndSetModImage(SkinModSettings skinModSettings)
    {
        var uri = Uri.TryCreate(skinModSettings.ImagePath, UriKind.Absolute, out var result) &&
                  result.Scheme == Uri.UriSchemeFile
            ? result
            : throw new ArgumentException("Invalid image path.", nameof(skinModSettings.ImagePath));

        if (!File.Exists(uri.LocalPath))
            throw new FileNotFoundException("Image file not found.", uri.LocalPath);

        // Delete old image
        if (!string.IsNullOrWhiteSpace(CachedSkinModSettings?.ImagePath))
        {
            var oldImagePath = Path.Combine(FullPath, CachedSkinModSettings.ImagePath);
            if (File.Exists(oldImagePath))
                File.Delete(oldImagePath);
        }


        var newImageFileName = ImageName + Path.GetExtension(uri.LocalPath);
        var newImagePath = Path.Combine(FullPath, newImageFileName);

        File.Copy(uri.LocalPath, newImagePath, true);
        skinModSettings.ImagePath = newImageFileName;
        return Task.CompletedTask;
    }

    public async Task SaveSkinModSettings(SkinModSettings skinModSettings,
        CancellationToken cancellationToken = default)
    {
        Refresh();

        if (CachedSkinModSettings is not null && skinModSettings.Equals(CachedSkinModSettings))
            return;

        if (!string.IsNullOrWhiteSpace(skinModSettings.ImagePath) && CachedSkinModSettings?.ImagePath != skinModSettings.ImagePath)
            await CopyAndSetModImage(skinModSettings);

        var options = new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true,
        };

        var json = JsonSerializer.Serialize(skinModSettings, options);
        await File.WriteAllTextAsync(_configFilePath, json, cancellationToken);

        await ReadSkinModSettings(true, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// This checks that the image path is a valid absolute path or a valid relative path to the mod folder. Also updates the image path if it's relative.
    /// </summary>
    private bool SetAbsoluteImagePath(SkinModSettings skinModSettings)
    {
        if (!string.IsNullOrWhiteSpace(skinModSettings.ImagePath))
        {
            if (File.Exists(skinModSettings.ImagePath)) // Is Absolute path
            {
                skinModSettings.ImagePath = skinModSettings.ImagePath;
                return true;
            }
            else // Is Relative to mod folder
            {
                var imagePath = Path.Combine(FullPath, skinModSettings.ImagePath);
                if (File.Exists(imagePath))
                {
                    skinModSettings.ImagePath = new Uri(imagePath).ToString();
                    return true;
                }   
            }
        }

        return false;
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

public class SkinModSettings // "internal set" messes with the json serializer
    : IEquatable<SkinModSettings>
{
    public bool Equals(SkinModSettings? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return CustomName == other.CustomName && Author == other.Author && Version == other.Version &&
               ModUrl == other.ModUrl && Path.GetFileName(ImagePath) == Path.GetFileName(other.ImagePath);
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is SkinModSettings other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(CustomName, Author, Version, ModUrl, ImagePath);
    }

    public string? CustomName { get; set; }
    public string? Author { get; set; }
    public string? Version { get; set; }
    public string? ModUrl { get; set; }
    public string? ImagePath { get; set; }
}

// There needs to be a better way to do this
public class SkinModKeySwap : IEquatable<SkinModKeySwap>
{
    public const string KeySwapIniSection = "KeySwap";
    public const string ConditionIniKey = "condition";
    public string? Condition { get; set; }
    public const string ForwardIniKey = "key";
    public string? ForwardHotkey { get; set; }
    public const string BackwardIniKey = "back";
    public string? BackwardHotkey { get; set; }
    public const string TypeIniKey = "type";
    public string? Type { get; set; }
    public const string SwapVarIniKey = "$swapvar";
    public string[]? SwapVar { get; set; }

    public bool Equals(SkinModKeySwap? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Condition == other.Condition && ForwardHotkey == other.ForwardHotkey &&
               BackwardHotkey == other.BackwardHotkey && Type == other.Type && Equals(SwapVar, other.SwapVar);
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is SkinModKeySwap other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Condition, ForwardHotkey, BackwardHotkey, Type, SwapVar);
    }
}