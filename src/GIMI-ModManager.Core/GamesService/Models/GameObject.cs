using GIMI_ModManager.Core.GamesService.Interfaces;

namespace GIMI_ModManager.Core.GamesService.Models;

public class GameObject : BaseModdableObject, IGameObject
{
    protected internal GameObject(IModdableObject moddableObject) : base(moddableObject)
    {
    }
}

public interface IGameObject : IModdableObject
{
}