using GIMI_ModManager.Core.Contracts.Entities;

namespace GIMI_ModManager.Core.Entities.Mods.Helpers;

internal static class ModsHelpers
{
    public static string? UriPathToModRelativePath(string modPath, string? uriPath)
    {
        if (string.IsNullOrWhiteSpace(uriPath))
            return null;

        if (Uri.IsWellFormedUriString(modPath, UriKind.Absolute) &&
            Uri.IsWellFormedUriString(uriPath, UriKind.Absolute))
        {
            var modUri = new Uri(modPath);

            var uri = new Uri(uriPath);

            var relativeUri = modUri.MakeRelativeUri(uri);
            return relativeUri.ToString();
        }


        if (Uri.IsWellFormedUriString(uriPath, UriKind.Absolute))
        {
            var filename = Path.GetFileName(uriPath);
            return string.IsNullOrWhiteSpace(filename) ? string.Empty : filename;
        }

        var absPath = Path.GetFileName(uriPath);

        var file = Path.GetFileName(absPath);
        return string.IsNullOrWhiteSpace(file) ? string.Empty : file;
    }

    public static Uri? RelativeModPathToAbsPath(string modPath, string? relativeModPath)
    {
        if (string.IsNullOrWhiteSpace(relativeModPath))
            return null;

        var uri = Uri.TryCreate(Path.Combine(modPath, relativeModPath), UriKind.Absolute, out var result) &&
                  result.Scheme == Uri.UriSchemeFile
            ? result
            : null;

        return uri;
    }

    public static bool IsInModFolder(ISkinMod mod, Uri path)
    {
        if (path.Scheme != Uri.UriSchemeFile)
            return false;

        var fsPath = path.LocalPath;
        return mod.FullPath.Contains(fsPath, StringComparison.OrdinalIgnoreCase);
    }


    public static Uri? StringUrlToUri(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        return Uri.IsWellFormedUriString(url, UriKind.Absolute) ? new Uri(url) : null;
    }
}