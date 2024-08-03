namespace GIMI_ModManager.WinUI.Helpers
{
    public static class FormaterHelpers
    {
        public static string FormatTimeSinceAdded(TimeSpan timeSinceAdded)
        {
            return timeSinceAdded switch
            {
                { Days: > 0 } => $"{Math.Round(timeSinceAdded.TotalDays)} days ago",
                { Hours: > 0 } => $"{timeSinceAdded.Hours} hours ago",
                { Minutes: > 0 } => $"{timeSinceAdded.Minutes} minutes ago",
                _ => $"{timeSinceAdded.Seconds} seconds ago"
            };
        }
    }
}