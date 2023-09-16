using Newtonsoft.Json;

namespace GIMI_ModManager.WinUI.Models.Options;

public class ModAttentionSettings
{
    [JsonIgnore] public const string Key = "ModAttentionSettings";

    public ModNotification[] ModNotifications { get; set; } = Array.Empty<ModNotification>();
}

public sealed class ModNotification
{
    public string ModCustomName { get; set; }
    public string ModFolderName { get; set; }
    public bool ShowOnOverview { get; set; }
    public AttentionType AttentionType { get; set; }
    public string Message { get; set; }
}

public enum AttentionType
{
    Added,
    Modified,
    UpdateAvailable,
    Error
}