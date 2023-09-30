using GIMI_ModManager.Core.Contracts.Entities;
using Newtonsoft.Json;

namespace GIMI_ModManager.WinUI.Models.Options;

public class ModAttentionSettings
{
    [JsonIgnore] public const string Key = "ModAttentionSettings";

    public Dictionary<int, IModNotification[]> ModNotifications { get; set; } = new();
}

public sealed class ModNotification : IModNotification
{
    public ModNotification()
    {
    }

    public Guid Id { get; init; } = Guid.NewGuid();
    public int CharacterId { get; init; } = -1;
    public string ModCustomName { get; init; } = string.Empty;
    public string ModFolderName { get; init; } = string.Empty;
    public string ModFolderPath { get; init; } = string.Empty;
    public bool ShowOnOverview { get; init; }
    public AttentionType AttentionType { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class SkinModNotification : IModNotification
{
    private readonly ISkinMod _skinMod;

    public SkinModNotification(ISkinMod skinMod, string message, AttentionType attentionType, int characterId = -1)
    {
        _skinMod = skinMod;
        Message = message;
        AttentionType = attentionType;
        CharacterId = characterId;
    }

    public Guid Id { get; } = Guid.NewGuid();
    public int CharacterId { get; }
    public string ModCustomName => _skinMod.CustomName;
    public string ModFolderName => _skinMod.Name;
    public string ModFolderPath => _skinMod.FullPath;
    public AttentionType AttentionType { get; }
    public string Message { get; }
}

public interface IModNotification
{
    public Guid Id { get; }
    public int CharacterId { get; }
    public string ModCustomName { get; }
    public string ModFolderName { get; }
    public string ModFolderPath { get; }
    public AttentionType AttentionType { get; }
    public string Message { get; }
}

public enum AttentionType
{
    None,
    Added,
    Modified,
    UpdateAvailable, // Also show in character overview
    Error // Also show in character overview
}