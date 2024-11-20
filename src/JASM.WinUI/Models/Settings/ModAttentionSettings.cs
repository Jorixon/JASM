using GIMI_ModManager.WinUI.Services.Notifications;
using Newtonsoft.Json;

namespace GIMI_ModManager.WinUI.Models.Settings;

public class ModAttentionSettings
{
    [JsonIgnore] public const string Key = "ModAttentionSettings";

    public Dictionary<string, ModNotification[]> ModNotifications { get; set; } = new();
}