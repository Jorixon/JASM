using System.Reflection.Metadata.Ecma335;
using System.Text.Json;
using GIMI_ModManager.Core.Contracts.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace GIMI_ModManager.Core.Entities;

public class SkinMod
{
    private readonly IMod _mod;
    private const string ModIniString = "merged.ini";
    private readonly string _modIniPath;
    private const string configFileName = ".JASM_ModConfig.json";
    private readonly string _configFilePath;
    public bool HasMergedInI { get; private set; }

    private readonly List<SkinModKeySwap> _keySwaps = new();
    public IReadOnlyList<SkinModKeySwap> KeySwaps => _keySwaps.AsReadOnly();


    public SkinMod(IMod mod)
    {
        var modFolderAttributes = File.GetAttributes(mod.FullPath);
        if (!modFolderAttributes.HasFlag(FileAttributes.Directory))
            throw new ArgumentException("Mod must be a folder.", nameof(mod));
        _mod = mod;
        IsValidFolder();
        HasMergedInI = HasMergedInIFile(_mod);
        _configFilePath = Path.Combine(_mod.FullPath, configFileName);
        _modIniPath = Path.Combine(_mod.FullPath, ModIniString);
    }

    public async Task Refresh()
    {
        if (!IsValidFolder())
            throw new InvalidOperationException("Mod folder is no longer valid.");
        HasMergedInI = HasMergedInIFile(_mod);
    }

    private static bool HasMergedInIFile(IMod mod) =>
        new DirectoryInfo(mod.FullPath).EnumerateFiles("*.ini", SearchOption.TopDirectoryOnly)
            .Any(iniFiles => iniFiles.Name.Equals(ModIniString, StringComparison.CurrentCultureIgnoreCase));

    public bool IsValidFolder()
    {
        return _mod.Exists() && !_mod.IsEmpty();
    }

    public async Task<OperationResult> ReadKeySwapConfiguration(CancellationToken cancellationToken = default)
    {
        await Refresh();
        if (!HasMergedInI)
            throw new InvalidOperationException("Mod has no merged.ini file.");

        List<string> keySwapLines = new();
        List<SkinModKeySwap> keySwaps = new();
        var keySwapBlockStarted = false;
        await foreach (var line in File.ReadLinesAsync(_configFilePath, cancellationToken))
        {
            if (line.StartsWith("[KeySwap]", StringComparison.CurrentCultureIgnoreCase) && !keySwapBlockStarted)
            {
                keySwapBlockStarted = false;
                var keySwap = ParseKeySwap(keySwapLines);
                if (keySwap is not null)
                    keySwaps.Add(keySwap);
                keySwapLines.Clear();
            }

            if (line.StartsWith("[KeySwap]", StringComparison.CurrentCultureIgnoreCase))
            {
                keySwapBlockStarted = true;
                continue;
            }

            if (keySwapBlockStarted)
                keySwapLines.Add(line);
        }

        _keySwaps.Clear();
        _keySwaps.AddRange(keySwaps);
        var message = $"Loaded {_keySwaps.Count} key swaps.";
        return new OperationResult(true, message);
    }

    private static SkinModKeySwap? ParseKeySwap(ICollection<string> fileLines)
    {
        var skinModKeySwap = new SkinModKeySwap();

        foreach (var line in fileLines)
        {
            if (IsIniKey(line, "key"))
                skinModKeySwap.ForwardHotkey = GetIniValue(line);

            else if (IsIniKey(line, "back"))
                skinModKeySwap.BackwardHotkey = GetIniValue(line);

            else if (IsIniKey(line, "type"))
                skinModKeySwap.Type = GetIniValue(line);

            else if (IsIniKey(line, "$swapvar"))
                skinModKeySwap.SwapVar = GetIniValue(line)?.Split(',');

            else if (IsIniKey(line, "condition"))
                skinModKeySwap.Condition = GetIniValue(line);
        }

        return skinModKeySwap;
    }

    private static string? GetIniValue(string line)
    {
        var split = line.Split('=');
        return split.Length != 2 ? null : split[1].Trim();
    }

    private static bool IsIniKey(string line, string key) =>
        line.Trim().StartsWith(key, StringComparison.CurrentCultureIgnoreCase);

    public async Task<SkinModSettings> ReadSkinModSettings(CancellationToken cancellationToken = default)
    {
        await Refresh();

        var fileContents = await File.ReadAllTextAsync(_configFilePath, cancellationToken);
        var options = new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        return JsonSerializer.Deserialize<SkinModSettings>(fileContents, options) ?? new SkinModSettings();
    }

    public async Task WriteSkinModSettings(SkinModSettings skinModSettings,
        CancellationToken cancellationToken = default)
    {
        await Refresh();

        var options = new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(skinModSettings, options);
        await File.WriteAllTextAsync(_configFilePath, json, cancellationToken);
    }
}

public record OperationResult(bool Success, string? Message = null);

public class SkinModSettings
{
    public string? CustomName { get; set; }
    public string? Author { get; set; }
    public string? Version { get; set; }
    public Uri? ModUrl { get; set; }
    public Uri? ImageUri { get; set; }
}

public class SkinModKeySwap
{
    public string? Condition { get; set; }
    public string? ForwardHotkey { get; set; }
    public string? BackwardHotkey { get; set; }
    public string? Type { get; set; }
    public string[]? SwapVar { get; set; }
}

public class MergedIniSettings
{
    public SkinModKeySwap[] KeySwaps { get; init; }
}