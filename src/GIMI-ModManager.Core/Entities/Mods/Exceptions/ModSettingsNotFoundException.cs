namespace GIMI_ModManager.Core.Entities.Mods.Exceptions;

public class ModSettingsNotFoundException : Exception
{
    public ModSettingsNotFoundException(string message) : base(message)
    {
    }
}