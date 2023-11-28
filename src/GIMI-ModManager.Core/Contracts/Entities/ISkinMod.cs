using GIMI_ModManager.Core.Entities.Mods.SkinMod;

namespace GIMI_ModManager.Core.Contracts.Entities;

public interface ISkinMod : IMod, IEqualityComparer<ISkinMod>, IEquatable<ISkinMod>
{
    Guid Id { get; }
    public string GetDisplayName();
    public bool HasMergedInI { get; }
    public void ClearCache();
    public SkinModSettingsManager Settings { get; }
    public SkinModKeySwapManager? KeySwaps { get; }
    public new ISkinMod CopyTo(string absPath);

    public bool ContainsOnlyJasmFiles();
    public string? GetModIniPath();

    // Get folder name without the disabled prefix
    public string GetNameWithoutDisabledPrefix();
}