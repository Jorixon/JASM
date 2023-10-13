using System.Diagnostics;

namespace GIMI_ModManager.Core.GamesService.Models;

[DebuggerDisplay("{" + nameof(DisplayName) + "}")]
internal class Region : IRegion
{
    public Region(string internalName, string displayName)
    {
        InternalName = internalName;
        DisplayName = displayName;
    }

    public string InternalName { get; set; }
    public string DisplayName { get; set; }
}