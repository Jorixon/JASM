using GIMI_ModManager.Core.GamesService.Models;
using GIMI_ModManager.Core.Helpers;
using Serilog;

namespace GIMI_ModManager.Core.GamesService;

internal static class MapperHelpers
{
    private static Uri? CombinePaths(string path1, string path2)
    {
        var imagePath = Path.Combine(path1, path2);

        var uri = Uri.TryCreate(imagePath, UriKind.Absolute, out var uriResult)
            ? uriResult
            : null;
        return uri;
    }

    internal static Uri? GetImageUri(InternalName internalName, string? imageFolderPath,
        ICategory? category = null, string? jsonImageFileName = null)
    {
        if (string.IsNullOrWhiteSpace(imageFolderPath))
            return null;


        if (string.IsNullOrWhiteSpace(jsonImageFileName) && category is not null)
        {
            var acceptedExtensions = Constants.SupportedImageExtensions;

            foreach (var acceptedExtension in acceptedExtensions)
            {
                var imageName = $"{category.InternalName}_{internalName.Id}{acceptedExtension}";

                if (!File.Exists(CombinePaths(imageFolderPath, imageName)?.LocalPath)) continue;

                jsonImageFileName = imageName;
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(jsonImageFileName))
            return null;


        var imageUri = CombinePaths(imageFolderPath, jsonImageFileName);

        if (imageUri is not null && File.Exists(imageUri.LocalPath))
        {
            return imageUri;
        }

        Log.Warning("Image for {InternalName} not found at {ImageUri}", internalName, imageUri);

        return null;
    }
}