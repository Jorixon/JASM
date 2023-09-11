using GIMI_ModManager.Core.Contracts.Entities;

namespace GIMI_ModManager.Core.Services;



public class ModManagerService
{


}
/// <summary>
/// This service is responsible for managing the internals of the mods. 
/// </summary>
// ??? And also in charge of keeping the identity of the mods. ???
public interface IModManagerService
{
    /// <summary>
    /// Checks if the mod is where it should be. And has the correct format.
    /// </summary>
    /// <param name="mod"></param>
    /// <returns></returns>
    public bool IsValidMod(IMod mod);


}