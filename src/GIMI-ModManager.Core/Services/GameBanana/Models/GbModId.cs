namespace GIMI_ModManager.Core.Services.GameBanana.Models;

/// <summary>
/// Example: https://gamebanana.com/apiv11/Mod/<see cref="GbModId"/>/ProfilePage
/// </summary>
public record GbModId
{
    public string ModId { get; }

    public GbModId(string modId)
    {
        ModId = modId;
    }

    public GbModId(int modId)
    {
        ModId = modId.ToString();
    }

    public override string ToString() => ModId;

    public static implicit operator string(GbModId modId) => modId.ToString();
}