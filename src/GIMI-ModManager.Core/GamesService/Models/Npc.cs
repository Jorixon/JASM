using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.GamesService.JsonModels;
using Serilog;

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
        npc.SetImageUri(imageFolder, jsonNpc.Image);


        return npc;
    }


    internal void SetImageUri(string imageFolder, string? jsonImagePath)
    {
        if (string.IsNullOrWhiteSpace(imageFolder) || string.IsNullOrWhiteSpace(jsonImagePath))
            return;

        var imagePath = Path.Combine(imageFolder, jsonImagePath);

        var imageUri = Uri.TryCreate(imagePath, UriKind.Absolute, out var uriResult)
            ? uriResult
            : null;

        if (imageUri is not null && File.Exists(imageUri.LocalPath))
        {
            ImageUri = imageUri;
            return;
        }

        Log.Warning("Image for {InternalName} not found at {ImageUri}", InternalName, imageUri);
    }


    public override string ToString() => $"{DisplayName} ({InternalName})";

    public bool Equals(IModdableObject? other) => InternalName.DefaultEquatable(this, other);

    public bool Equals(INameable? other) => InternalName.DefaultEquatable(this, other);

    public bool Equals(INpc? other) => InternalName.DefaultEquatable(this, other);
}