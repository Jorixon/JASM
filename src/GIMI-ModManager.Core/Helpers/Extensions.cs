namespace GIMI_ModManager.Core.Helpers;

public static class Extensions
{
    public static bool AbsPathCompare(this string absPath, string absOtherPath)
    {
        return Path.GetFullPath(absPath).Equals(Path.GetFullPath(absOtherPath), StringComparison.OrdinalIgnoreCase);
    }

    public static void ForEach<T>(this IEnumerable<T> enumerable, Action<T> action)
    {
        foreach (var item in enumerable)
            action(item);
    }
}