using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.GamesService.JsonModels;

namespace GIMI_ModManager.Core.GamesService.Models;

public class Weapon : BaseModdableObject, IWeapon
{
    public int Rarity { get; private init; }
    public IGameClass GameClass { get; }

    private Weapon(IModdableObject moddableObject, IGameClass gameClass) : base(moddableObject)
    {
        GameClass = gameClass;
    }

    internal static IWeapon FromJson(JsonWeapon json, string imageFolder, int rarity,
        IEnumerable<IGameClass> gameClasses)
    {
        var gameClass = gameClasses.FirstOrDefault(x => x.InternalName.Equals(json.Type)) ?? Class.NoneClass();
        return new Weapon(FromJson(json, Category.CreateForWeapons(), imageFolder), gameClass)
        {
            Rarity = rarity
        };
    }
}