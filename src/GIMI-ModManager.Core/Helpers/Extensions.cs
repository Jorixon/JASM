using System.Diagnostics.CodeAnalysis;

namespace GIMI_ModManager.Core.Helpers;

public static class Extensions
{
    /// <summary>
    /// Compares two absolute paths, ignoring case and trailing directory separators.
    /// </summary>
    /// <param name="absPath"></param>
    /// <param name="absOtherPath"></param>
    /// <returns></returns>
    public static bool AbsPathCompare(this string absPath, string absOtherPath)
    {
        return Path.GetFullPath(absPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Equals(
                Path.GetFullPath(absOtherPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.CurrentCultureIgnoreCase);
    }

    /// <inheritdoc cref="List{T}.ForEach"/>
    public static void ForEach<T>(this IEnumerable<T> enumerable, Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(enumerable);
        ArgumentNullException.ThrowIfNull(action);

        if (enumerable is List<T> list)
            list.ForEach(action);
        else
            foreach (var item in enumerable)
                action(item);
    }

    /// <summary>
    /// Returns true if the string is null or whitespace.
    /// </summary>
    public static bool IsNullOrEmpty([NotNullWhen(false)] this string? value)
    {
        return string.IsNullOrWhiteSpace(value);
    }
}