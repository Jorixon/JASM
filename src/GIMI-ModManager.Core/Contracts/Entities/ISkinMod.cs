using GIMI_ModManager.Core.Entities;

namespace GIMI_ModManager.Core.Contracts.Entities;

public interface ISkinMod : IMod, IEqualityComparer<ISkinMod>
{
    public SkinModSettings? CachedSkinModSettings { get; }
    public IReadOnlyCollection<SkinModKeySwap>? CachedKeySwaps { get; }
    public bool HasMergedInI { get; }

    public Task<IReadOnlyCollection<SkinModKeySwap>> ReadKeySwapConfiguration(
        CancellationToken cancellationToken = default);

    public Task SaveKeySwapConfiguration(ICollection<SkinModKeySwap> updatedKeySwaps,
        CancellationToken cancellationToken = default);

    public Task SetModImage(string imagePath);

    public Task<SkinModSettings> ReadSkinModSettings(CancellationToken cancellationToken = default);

    public Task SaveSkinModSettings(SkinModSettings skinModSettings,
        CancellationToken cancellationToken = default);
}