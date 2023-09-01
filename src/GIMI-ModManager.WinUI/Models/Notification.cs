namespace GIMI_ModManager.WinUI.Models;

public record Notification
{
    public Notification(string title, string message)
    {
        Title = title;
        Message = message;
        Timestamp = DateTime.Now;
    }

    public string Title { get; }
    public string Message { get; }
    public DateTime Timestamp { get; }
}