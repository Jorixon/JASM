using GIMI_ModManager.Core.Entities.Mods.FileModels;

namespace GIMI_ModManager.Core.Helpers;

// This class just holds code that i don't know where to put yet.
public static class IniConfigHelpers
{
    public static IniKeySwapSection? ParseKeySwap(ICollection<string> fileLines, string sectionLine)
    {
        var skinModKeySwap = new IniKeySwapSection
        {
            SectionKey = sectionLine.Trim()
        };

        foreach (var line in fileLines)
        {
            if (IsIniKey(line, IniKeySwapSection.ForwardIniKey))
                skinModKeySwap.ForwardHotkey = GetIniValue(line);

            else if (IsIniKey(line, IniKeySwapSection.BackwardIniKey))
                skinModKeySwap.BackwardHotkey = GetIniValue(line);

            else if (IsIniKey(line, IniKeySwapSection.TypeIniKey))
                skinModKeySwap.Type = GetIniValue(line);

            else if (IsIniKey(line, IniKeySwapSection.SwapVarIniKey))
                skinModKeySwap.SwapVar = GetIniValue(line)?.Split(',');

            else if (IsSection(line))
                break;
        }

        var result = skinModKeySwap.AnyValues() ? skinModKeySwap : null;
        return result;
    }

    public static string? GetIniValue(string line)
    {
        if (IsComment(line)) return null;

        var split = line.Split('=');

        if (split.Length <= 2) return split.Length != 2 ? null : split[1].Trim();


        split[1] = string.Join("", split.Skip(1));
        return split[1].Trim();
    }

    public static string? GetIniKey(string line)
    {
        if (IsComment(line)) return null;

        var split = line.Split('=');
        return split.Length != 2 ? split.FirstOrDefault()?.Trim() : split[0].Trim();
    }

    public static bool IsComment(string line) => line.Trim().StartsWith(";");

    public static bool IsSection(string line, string? sectionKey = null)
    {
        line = line.Trim();
        if (sectionKey is null && line.StartsWith("[") && line.EndsWith("]"))
            return true;

        if (sectionKey is not null && line.Equals($"[{sectionKey}]", StringComparison.CurrentCultureIgnoreCase))
            return true;

        if (sectionKey is not null && (sectionKey.StartsWith("[") && sectionKey.EndsWith("]")) &&
            line.Equals(sectionKey, StringComparison.CurrentCultureIgnoreCase))
            return true;

        return false;
    }

    public static bool IsIniKey(string line, string key) =>
        line.Trim().StartsWith(key, StringComparison.CurrentCultureIgnoreCase);

    public static string? FormatIniKey(string key, string? value) =>
        value is not null ? $"{key} = {value}" : null;
}