namespace GIMI_ModManager.WinUI.Services;

// This is a static class to easily  launch a not implemented notification from different places in the app.
internal static class NotImplemented
{
    public static NotificationManager NotificationManager { get; set; } = null!;

    public static void Show(string? message = null, TimeSpan? time = null)
    {
        NotificationManager.ShowNotification("Not Implemented", message ?? "This feature is not implemented yet.",
            time ?? TimeSpan.FromSeconds(2));
    }
}