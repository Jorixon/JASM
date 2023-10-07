using GIMI_ModManager.Core.Entities.Mods.SkinMod;

namespace GIMI_ModManager.Core.Contracts.Entities;

public interface ISkinMod : IMod, IEqualityComparer<ISkinMod>
{
    Guid Id { get; }
    public bool HasMergedInI { get; }
    public void ClearCache();
    public SkinModSettingsManager Settings { get; }
    public SkinModKeySwapManager? KeySwaps { get; }
}