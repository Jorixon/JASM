using GIMI_ModManager.Core.Entities;

namespace GIMI_ModManager.Core.Helpers;

// This class just holds code that i don't know where to put yet.
public static class IniConfigHelpers
{
    public static SkinModKeySwap? ParseKeySwap(ICollection<string> fileLines)
    {
        var skinModKeySwap = new SkinModKeySwap();

        foreach (var line in fileLines)
        {
            if (IsIniKey(line, SkinModKeySwap.ForwardIniKey))
                skinModKeySwap.ForwardHotkey = GetIniValue(line);

            else if (IsIniKey(line, SkinModKeySwap.BackwardIniKey))
                skinModKeySwap.BackwardHotkey = GetIniValue(line);

            else if (IsIniKey(line, SkinModKeySwap.TypeIniKey))
                skinModKeySwap.Type = GetIniValue(line);

            else if (IsIniKey(line, SkinModKeySwap.SwapVarIniKey))
                skinModKeySwap.SwapVar = GetIniValue(line)?.Split(',');

            else if (IsIniKey(line, SkinModKeySwap.ConditionIniKey))
                skinModKeySwap.Condition = GetIniValue(line);
            else if (IsSection(line))
                break;
        }

        return skinModKeySwap;
    }

    public static string? GetIniValue(string line)
    {
        var split = line.Split('=');
        return split.Length != 2 ? null : split[1].Trim();
    }

    public static bool IsSection(string line, string? sectionKey = null)
    {
        line = line.Trim();
        if (!line.StartsWith("[") && !line.EndsWith("]"))
            return false;


        return sectionKey is null || line.Equals($"[{sectionKey}]", StringComparison.CurrentCultureIgnoreCase) || line.Equals($"{sectionKey}", StringComparison.CurrentCultureIgnoreCase);
    }

    public static bool IsIniKey(string line, string key) =>
        line.Trim().StartsWith(key, StringComparison.CurrentCultureIgnoreCase);

    public static string? FormatIniKey(string key, string? value) =>
        value is not null ? $"{key} = {value}" : null;
}