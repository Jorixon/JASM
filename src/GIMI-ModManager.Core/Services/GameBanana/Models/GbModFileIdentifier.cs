namespace GIMI_ModManager.Core.Services.GameBanana.Models;

/// <summary>
/// Represents a unique identifier for a mod file on GameBanana.
/// </summary>
public record GbModFileIdentifier(GbModId ModId, GbModFileId ModFileId);