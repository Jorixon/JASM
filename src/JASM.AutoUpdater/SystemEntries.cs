namespace JASM.AutoUpdater;

public static class SystemEntries
{
    // These are entries that stop the install as they are system files/folders or user files/folders
    // that should never be touched by the installer.
    public static string[] WindowsEntries = new[]
    {
        "Windows",
        "Program Files",
        "Program Files (x86)",
        "ProgramData",
        "System32",
        "SysWOW64",
        "Documents",
        "Desktop",
        "AppData",
        "Music",
        "Videos",
        "Pictures",
        "Downloads",
        "OneDrive"
    };
}