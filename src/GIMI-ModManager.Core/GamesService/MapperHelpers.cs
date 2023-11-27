using GIMI_ModManager.Core.GamesService.Models;
using Serilog;

namespace GIMI_ModManager.Core.GamesService;

internal static class MapperHelpers
{
    internal static Uri? GetImageUri(InternalName internalName, string? imageFolder, string? jsonImagePath)
    {
        if (string.IsNullOrWhiteSpace(jsonImagePath) || string.IsNullOrWhiteSpace(imageFolder))
            return null;

        var imagePath = Path.Combine(imageFolder, jsonImagePath);

        var imageUri = Uri.TryCreate(imagePath, UriKind.Absolute, out var uriResult)
            ? uriResult
            : null;

        if (imageUri is not null && File.Exists(imageUri.LocalPath))
        {
            return imageUri;
        }

        Log.Warning("Image for {InternalName} not found at {ImageUri}", internalName, imageUri);

        return null;
    }
}