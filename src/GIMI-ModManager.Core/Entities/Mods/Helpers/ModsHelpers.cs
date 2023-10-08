using GIMI_ModManager.Core.Contracts.Entities;

namespace GIMI_ModManager.Core.Entities.Mods.Helpers;

internal static class ModsHelpers
{
    public static string? UriPathToModRelativePath(ISkinMod mod, string? uriPath)
    {
        if (string.IsNullOrWhiteSpace(uriPath))
            return null;

        var modPath = mod.FullPath;

        var modUri = Uri.TryCreate(modPath, UriKind.Absolute, out var result) &&
                     result.Scheme == Uri.UriSchemeFile
            ? result
            : null;

        var uri = Uri.TryCreate(uriPath, UriKind.Absolute, out var uriResult) &&
                  uriResult.Scheme == Uri.UriSchemeFile
            ? uriResult
            : null;

        // This is technically the only path that should be used.
        if (modUri is not null && uri is not null)
        {
            var relativeUri = modUri.MakeRelativeUri(uri);

            var modName = modUri.Segments.LastOrDefault();
            if (string.IsNullOrWhiteSpace(modName))
                modName = mod.Name;

            var relativePath = relativeUri.ToString().Replace($"{modName}/", "");

            return relativePath;
        }


        if (Uri.IsWellFormedUriString(uriPath, UriKind.Absolute))
        {
            var filename = Path.GetFileName(uriPath);
            return string.IsNullOrWhiteSpace(filename) ? null : filename;
        }

        var absPath = Path.GetFileName(uriPath);

        var file = Path.GetFileName(absPath);
        return string.IsNullOrWhiteSpace(file) ? null : file;
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


        return fsPath.StartsWith(mod.FullPath, StringComparison.OrdinalIgnoreCase);
    }


    public static Uri? StringUrlToUri(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        return Uri.IsWellFormedUriString(url, UriKind.Absolute) ? new Uri(url) : null;
    }

    public static Guid StringToGuid(string? guid)
    {
        if (string.IsNullOrWhiteSpace(guid))
            return Guid.NewGuid();

        return Guid.TryParse(guid, out var result) ? result : Guid.NewGuid();
    }
}