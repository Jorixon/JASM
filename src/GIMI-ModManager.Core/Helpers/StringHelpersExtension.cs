namespace GIMI_ModManager.Core.Helpers;

public static class StringHelpersExtension
{
    public static bool AbsPathCompare(this string absPath, string absOtherPath)
    {
        return Path.GetFullPath(absPath).Equals(Path.GetFullPath(absOtherPath), StringComparison.OrdinalIgnoreCase);
    }
}