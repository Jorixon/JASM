using System.Diagnostics;

namespace GIMI_ModManager.Core.GamesService.Models;

[DebuggerDisplay("{" + nameof(DisplayName) + "}")]
internal class Element : IGameElement
{
    public Element()
    {
    }

    public InternalName InternalName { get; init; } = null!;
    public string DisplayName { get; set; } = null!;
    public Uri? ImageUri { get; set; } = null;


    public static Element NoneElement()
    {
        return new Element
        {
            InternalName = new InternalName("None"),
            DisplayName = "None",
            ImageUri = null
        };
    }

    public bool Equals(INameable? other) => InternalName.DefaultEquatable(this, other);
}