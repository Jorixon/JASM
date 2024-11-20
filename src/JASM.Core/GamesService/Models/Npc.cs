using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.GamesService.JsonModels;

namespace GIMI_ModManager.Core.GamesService.Models;

public class Npc : INpc
{
    public string DisplayName { get; set; }
    public InternalName InternalName { get; init; }
    public Uri? ImageUri { get; set; }
    public string ModFilesName { get; internal set; } = string.Empty;
    public bool IsMultiMod { get; internal set; }
    public DateTime? ReleaseDate { get; set; } = DateTime.MinValue;
    public ICategory ModCategory { get; internal init; } = Category.CreateForNpc();
    public INpc DefaultNPC { get; internal init; } = null!;
    public ICollection<IRegion> Regions { get; internal set; } = Array.Empty<IRegion>();

    private Npc(InternalName internalName, string displayName)
    {
        InternalName = internalName;
        DisplayName = displayName;
    }

    internal static Npc FromJson(JsonNpc jsonNpc, string imageFolder)
    {
        var internalNameString = jsonNpc.InternalName ??
                                 throw new Character.InvalidJsonConfigException(
                                     "InternalName can never be missing or null");

        var internalName = new InternalName(internalNameString);

        var npc = new Npc(internalName, jsonNpc.DisplayName ?? internalName)
        {
            ModFilesName = jsonNpc.ModFilesName ?? string.Empty,
            IsMultiMod = jsonNpc.IsMultiMod ?? false
        };

        npc.ImageUri = MapperHelpers.GetImageUri(internalName, imageFolder, npc.ModCategory, jsonNpc.Image);

        return npc;
    }


    public override string ToString() => $"{DisplayName} ({InternalName})";

    public bool Equals(IModdableObject? other) => InternalName.DefaultEquatable(this, other);

    public bool Equals(INameable? other) => InternalName.DefaultEquatable(this, other);

    public bool Equals(INpc? other) => InternalName.DefaultEquatable(this, other);
}