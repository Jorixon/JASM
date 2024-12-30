using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.GamesService.JsonModels;
using GIMI_ModManager.Core.Helpers;

namespace GIMI_ModManager.Core.GamesService.Models;

/// <summary>
/// This is the minimal implementation of <see cref="IModdableObject"/>. It can be used as a base class for other implementations.
/// This class is not abstract because it can be used on its own as a custom moddable object.
/// </summary>
public class BaseModdableObject : IModdableObject
{
    public string DisplayName { get; set; }
    public InternalName InternalName { get; init; }

    public Uri? ImageUri { get; set; }
    public string ModFilesName { get; set; } = string.Empty;
    public bool IsMultiMod { get; set; }
    public ICategory ModCategory { get; }
    public bool IsCustomModObject { get; init; }

    private BaseModdableObject(InternalName internalName, string displayName, ICategory modCategory)
    {
        InternalName = internalName;
        DisplayName = displayName;
        ModCategory = modCategory;
    }

    protected BaseModdableObject(IModdableObject moddableObject)
    {
        InternalName = moddableObject.InternalName;
        DisplayName = moddableObject.DisplayName;
        ModCategory = moddableObject.ModCategory;
        ImageUri = moddableObject.ImageUri;
        ModFilesName = moddableObject.ModFilesName;
        IsMultiMod = moddableObject.IsMultiMod;
    }


    internal static BaseModdableObject FromJson(JsonBaseModdableObject json, ICategory category, string imageFolder, bool isCustomModObject = false)
    {
        var internalNameString = json.InternalName ??
                                 throw new Character.InvalidJsonConfigException(
                                     "InternalName can never be missing or null");

        var internalName = new InternalName(internalNameString);

        return new BaseModdableObject(
            internalName,
            json.DisplayName.IsNullOrEmpty() ? internalNameString : json.DisplayName,
            category
        )
        {
            ImageUri = MapperHelpers.GetImageUri(internalName, imageFolder, category, jsonImageFileName: json.Image),
            ModFilesName = json.ModFilesName ?? string.Empty,
            IsMultiMod = json.IsMultiMod ?? false,
            IsCustomModObject = isCustomModObject
        };
    }

    public bool Equals(IModdableObject? other)
    {
        return InternalName.DefaultEquatable(this, other);
    }

    public bool Equals(INameable? other)
    {
        return InternalName.DefaultEquatable(this, other);
    }
}