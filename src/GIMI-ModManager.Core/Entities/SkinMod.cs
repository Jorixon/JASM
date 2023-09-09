using System.Text.Json;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Helpers;
using SharpCompress.IO;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace GIMI_ModManager.Core.Entities;

public class SkinMod : Mod, ISkinMod
{
    private const string ModIniName = "merged.ini";
    private string _modIniPath = string.Empty;
    private const string configFileName = ".JASM_ModConfig.json";
    private string _configFilePath = string.Empty;
    private readonly List<SkinModKeySwap>? _keySwaps = new();

    public IReadOnlyList<SkinModKeySwap> KeySwaps => _keySwaps?.AsReadOnly() ?? new List<SkinModKeySwap>().AsReadOnly();
    public SkinModSettings? CachedSkinModSettings { get; } = null;
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
        Refresh();
    }

    public void Refresh()
    {
        _modDirectory.Refresh();
        if (!IsValidFolder())
            throw new InvalidOperationException("Mod folder is no longer valid.");

        _configFilePath = Path.Combine(FullPath, configFileName);
        _modIniPath = Path.Combine(FullPath, ModIniName);

        HasMergedInI = HasMergedInIFile();
    }

    private bool HasMergedInIFile() =>
        _modDirectory.EnumerateFiles("*.ini", SearchOption.TopDirectoryOnly)
            .Any(iniFiles => iniFiles.Name.Equals(ModIniName, StringComparison.CurrentCultureIgnoreCase));

    public bool IsValidFolder() => Exists() && !IsEmpty();

    public async Task<IReadOnlyCollection<SkinModKeySwap>> ReadKeySwapConfiguration(
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

        _keySwaps.Clear();
        _keySwaps.AddRange(keySwaps);
        return KeySwaps;
    }

    // It is what it is
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

        await using var writer = new StreamWriter(writeStream);

        foreach (var line in fileLines)
            await writer.WriteLineAsync(line);
    }


    public async Task<SkinModSettings> ReadSkinModSettings(CancellationToken cancellationToken = default)
    {
        Refresh();

        if (!File.Exists(_configFilePath))
            return new SkinModSettings();

        var fileContents = await File.ReadAllTextAsync(_configFilePath, cancellationToken);
        var options = new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        return JsonSerializer.Deserialize<SkinModSettings>(fileContents, options) ?? new SkinModSettings();
    }


    public async Task SetModImage(string imagePath)
    {
        var uri = Uri.TryCreate(imagePath, UriKind.Absolute, out var result) && result.Scheme == Uri.UriSchemeFile
            ? result
            : throw new ArgumentException("Invalid image path.", nameof(imagePath));

        if (!File.Exists(uri.LocalPath))
            throw new FileNotFoundException("Image file not found.", uri.LocalPath);

        var skinModSettings = CachedSkinModSettings ?? await ReadSkinModSettings();

        skinModSettings.ImagePath = uri.ToString();
        await SaveSkinModSettings(skinModSettings).ConfigureAwait(false);
    }

    public async Task SaveSkinModSettings(SkinModSettings skinModSettings,
        CancellationToken cancellationToken = default)
    {
        Refresh();

        var options = new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true,
        };

        var json = JsonSerializer.Serialize(skinModSettings, options);
        await File.WriteAllTextAsync(_configFilePath, json, cancellationToken);
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

public record OperationResult(bool Success, string? Message = null);

public class SkinModSettings // Setting internal sett messes with the json serializer
{
    public string? CustomName { get; set; }
    public string? Author { get; set; }
    public string? Version { get; set; }
    public string? ModUrl { get; set; }
    public string? ImagePath { get; set; }
}

// There needs to be a better way to do this
public class SkinModKeySwap
{
    public const string KeySwapIniSection = "KeySwap";
    public const string ConditionIniKey = "condition";
    public string? Condition { get; internal set; }
    public const string ForwardIniKey = "key";
    public string? ForwardHotkey { get; internal set; }
    public const string BackwardIniKey = "back";
    public string? BackwardHotkey { get; internal set; }
    public const string TypeIniKey = "type";
    public string? Type { get; internal set; }
    public const string SwapVarIniKey = "$swapvar";
    public string[]? SwapVar { get; internal set; }
}