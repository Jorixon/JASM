using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.Helpers;

namespace GIMI_ModManager.Core.GamesService.Models;

public class Category : ICategory
{
    public string DisplayName { get; set; }
    public string DisplayNamePlural { get; set; }
    public InternalName InternalName { get; init; }
    public ModCategory ModCategory { get; init; }
    public Type ModdableObjectType { get; }


    private Category(InternalName internalName, ModCategory modCategory, string displayName, string displayNamePlural,
        Type moddableObjectType)
    {
        ModCategory = modCategory;
        DisplayName = displayName;
        DisplayNamePlural = displayNamePlural;
        ModdableObjectType = moddableObjectType;
        InternalName = internalName;
    }


    public bool Equals(ICategory? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return InternalName.Equals(other.InternalName);
    }

    public bool Equals(INameable? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return InternalName.Equals(other.InternalName);
    }

    public static ICategory CreateForCharacter()
    {
        var internalName = new InternalName("Character");

        return new Category(internalName, ModCategory.Character, "Character", "Characters", typeof(ICharacter));
    }

    internal static ICategory CreateForNpc()
    {
        var internalName = new InternalName("NPC");

        return new Category(internalName, ModCategory.NPC, "NPC", "NPCs", typeof(INpc));
    }

    internal static ICategory CreateForObjects()
    {
        var internalName = new InternalName("Object");

        return new Category(internalName, ModCategory.Object, "Object", "Objects", typeof(IGameObject));
    }

    internal static ICategory CreateDefaultCustom()
    {
        var internalName = new InternalName("Custom");

        return new Category(internalName, ModCategory.Custom, "Custom", "Custom", typeof(IModdableObject));
    }

    internal static ICategory CreateCustom(string internalNameString, string displayName, string displayNamePlural)
    {
        var internalName = new InternalName(internalNameString);
        displayName = displayName.IsNullOrEmpty() ? internalNameString : displayName;
        displayNamePlural = displayNamePlural.IsNullOrEmpty() ? displayName : displayNamePlural;

        var categories = Enum.GetValues<ModCategory>().Select(categoryEnum => categoryEnum.ToString().ToLower());

        if (categories.Contains(internalName.Id))
            throw new ArgumentException($"The internal name '{internalName}' is already used internally in JASM");


        return new Category(internalName, ModCategory.Custom, displayName, displayNamePlural,
            typeof(IModdableObject));
    }
}