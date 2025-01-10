using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.GamesService.Models;

namespace GIMI_ModManager.Core.GamesService.Exceptions;

public class ModObjectNotFoundException : GameServiceException
{
    public ModObjectNotFoundException(string message) : base(message)
    {
    }

    public ModObjectNotFoundException(string message, Exception? innerException) : base(message, innerException)
    {
    }

    public static ModObjectNotFoundException Create<T>(InternalName internalName, Exception? innerException = null) where T : IModdableObject
    {
        return new ModObjectNotFoundException($"Moddable object of type {typeof(T).Name} with internal name {internalName} not found", innerException);
    }
}