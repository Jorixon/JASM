using System.Diagnostics;

namespace GIMI_ModManager.Core.GamesService.Models;

[DebuggerDisplay("{" + nameof(DisplayName) + "}")]
internal class Class : IGameClass
{
    public Class()
    {
    }

    public InternalName InternalName { get; init; } = null!;
    public string DisplayName { get; set; } = null!;
    public Uri? ImageUri { get; set; } = null;

    public static Class NoneClass()
    {
        return new Class
        {
            InternalName = new InternalName("None"),
            DisplayName = "None",
            ImageUri = null
        };
    }

    public bool Equals(INameable? other) => InternalName.DefaultEquatable(this, other);
}