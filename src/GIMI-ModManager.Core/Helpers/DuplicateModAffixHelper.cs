using System.Text.RegularExpressions;

namespace GIMI_ModManager.Core.Helpers;

public static partial class DuplicateModAffixHelper
{
    [GeneratedRegex(@"__\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex DuplicateModAffix();

    /// <summary>
    /// Tries to append a number to the end of the string, if it already has a number, it increments it.
    /// </summary>
    /// <param name="name">Some string with or without a number at the end</param>
    /// <returns>New string with a number or incremented number</returns>
    public static string AppendNumberAffix(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (DuplicateModAffix().IsMatch(name))
        {
            var strings = name.Split("__");
            if (strings.Length < 2)
                goto end;

            if (!int.TryParse(strings.Last(), out var number))
                goto end;

            number++;
            name = string.Join("__", strings[..^1]) + "__" + number;
            return name;
        }

        end:
        return name + "__1";
    }
}