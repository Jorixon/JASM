using Newtonsoft.Json;

namespace GIMI_ModManager.WinUI.Models.Options;

public class ModAttentionSettings
{
    [JsonIgnore] public const string Key = "ModAttentionSettings";

    public Dictionary<int, ModNotification[]> ModNotifications { get; set; } = new ();
}

public sealed class ModNotification
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public int CharacterId { get; init; }
    public string ModCustomName { get; init; } = string.Empty;
    public string ModFolderName { get; init; } = string.Empty;
    public bool ShowOnOverview { get; init; }
    public AttentionType AttentionType { get; init; }
    public string Message { get; init; } = string.Empty;
}

public enum AttentionType
{
    None,
    Added,
    Modified,
    UpdateAvailable, // Also show in character overview
    Error // Also show in character overview
}