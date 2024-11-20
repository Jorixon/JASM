using System.Diagnostics.CodeAnalysis;
using GIMI_ModManager.Core.Services.GameBanana.Models;

namespace GIMI_ModManager.Core.Services.GameBanana;

public static class GameBananaUrlHelper
{
    public static bool TryGetModIdFromUrl(Uri url, [NotNullWhen(true)] out GbModId? modId)
    {
        modId = null;

        if (url.Host != "gamebanana.com" || url.Scheme != Uri.UriSchemeHttps)
            return false;


        var segments = url.Segments;

        if (segments.Length < 2)
            return false;

        modId = new GbModId(segments.Last());

        if (modId.ModId.Contains('/'))
            return false;

        return true;
    }

}