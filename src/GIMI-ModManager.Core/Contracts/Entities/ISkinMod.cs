using GIMI_ModManager.Core.Entities;

namespace GIMI_ModManager.Core.Contracts.Entities;

public interface ISkinMod : IMod, IEqualityComparer<ISkinMod>
{
    public IReadOnlyCollection<string> ImagePaths { get; } // Support multiple images at some point ???
    public SkinModSettings? CachedSkinModSettings { get; }
    public IReadOnlyCollection<SkinModKeySwap>? CachedKeySwaps { get; }
    public bool HasMergedInI { get; }

    public void ClearCache();

    public Task<IReadOnlyCollection<SkinModKeySwap>> ReadKeySwapConfiguration(bool forceReload = false,
        CancellationToken cancellationToken = default);

    public Task SaveKeySwapConfiguration(ICollection<SkinModKeySwap> updatedKeySwaps,
        CancellationToken cancellationToken = default);


    public Task<SkinModSettings> ReadSkinModSettings(bool forceReload = false, CancellationToken cancellationToken = default);

    public Task SaveSkinModSettings(SkinModSettings skinModSettings,
        CancellationToken cancellationToken = default);
}