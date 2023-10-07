namespace GIMI_ModManager.WinUI.Services.Notifications;

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