using System.Diagnostics;

namespace GIMI_ModManager.Core.GamesService.Models;

[DebuggerDisplay("{" + nameof(DisplayName) + "}")]
internal class Element : IGameElement
{
    public Element()
    {
    }

    public string InternalName { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public Uri? ImageUri { get; set; } = null;
}