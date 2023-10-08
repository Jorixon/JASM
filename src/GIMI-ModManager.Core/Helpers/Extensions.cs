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

    public static void ForEach<T>(this IEnumerable<T> enumerable, Action<T> action)
    {
        foreach (var item in enumerable)
            action(item);
    }
}