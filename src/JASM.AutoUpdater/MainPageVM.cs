using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;

namespace JASM.AutoUpdater;

public partial class MainPageVM : ObservableRecipient
{
    private readonly string WorkDir = Path.Combine(Path.GetTempPath(), "JASM_Auto_Updater");
    private string _zipPath = string.Empty;
    private DirectoryInfo _extractedJasmFolder = null!;
    private DirectoryInfo _installedJasmFolder = null!;
    private string _newJasmExePath = string.Empty;

    private readonly string _7zPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Assets\7z\", "7z.exe");

    [ObservableProperty] private bool _inStartupView = true;

    [ObservableProperty] private bool _updateProcessStarted = false;
    [ObservableProperty] private string _latestVersion = "-----";
    [ObservableProperty] private Uri _defaultBrowserUri = new("https://github.com/Jorixon/JASM/releases");

    public ObservableCollection<LogEntry> ProgressLog { get; } = new();

    public UpdateProgress UpdateProgress { get; } = new();

    public Version InstalledVersion { get; }

    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private bool _finishedSuccessfully = false;

    [ObservableProperty] private bool _stopped;
    [ObservableProperty] private string? _stopReason;

    public MainPageVM(string installedJasmVersion)
    {
        InstalledVersion = Version.TryParse(installedJasmVersion, out var version) ? version : new Version(0, 0, 0, 0);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task StartUpdateAsync(CancellationToken cancellationToken)
    {
        UpdateProgress.Reset();
        IsLoading = true;
        InStartupView = false;
        UpdateProcessStarted = true;
        Stopped = false;
        StopReason = null;

        Log(InstalledVersion.Equals(new Version(0, 0, 0, 0))
            ? "Could not determine installed JASM version..."
            : $"Installed JASM version: {InstalledVersion}");


        try
        {
            var release = await IsNewerVersionAvailable(cancellationToken);
            UpdateProgress.NextStage();
            if (Stopped || release is null)
                return;

            await Task.Delay(1000, cancellationToken);
            await DownloadLatestVersion(release, cancellationToken);
            UpdateProgress.NextStage();

            if (Stopped)
            {
                CleanUp();
                return;
            }

            await Task.Delay(1000, cancellationToken);
            await UnzipLatestVersion(cancellationToken);
            UpdateProgress.NextStage();

            if (Stopped)
            {
                CleanUp();
                return;
            }

            await Task.Delay(1000, cancellationToken);
            await InstallLatestVersion();
            if (Stopped)
            {
                CleanUp();
                return;
            }

            UpdateProgress.NextStage();
        }
        catch (TaskCanceledException e)
        {
            Stop("User cancelled");
        }
        catch (OperationCanceledException e)
        {
            Stop("User cancelled");
        }
        catch (Exception e)
        {
            Log("An error occurred!", e.Message);
            Serilog.Log.Error(e, "An error occurred! Full error");
            Stop(e.Message);
        }
        finally
        {
            IsLoading = false;
        }

        if (Stopped)
        {
            CleanUp();
            return;
        }

        CleanUp();
        Finish();
    }


    private void Finish()
    {
        IsLoading = false;
        FinishedSuccessfully = true;
    }

    private async Task<GitHubRelease?> IsNewerVersionAvailable(CancellationToken cancellationToken)
    {
        var newestVersionFound = await GetLatestVersionAsync(cancellationToken);

        Log($"Newest version found: {newestVersionFound?.tag_name}");

        var release = new GitHubRelease()
        {
            Version = new Version(newestVersionFound?.tag_name?.Trim('v') ?? ""),
            PreRelease = newestVersionFound?.prerelease ?? false,
            PublishedAt = newestVersionFound?.published_at ?? DateTime.MinValue
        };

        if (release.Version <= InstalledVersion)
        {
            Stop("Installed version is newer than or equal to the newest version found on GitHub");
            return null;
        }

        var getJasmAsset = newestVersionFound?.assets?.FirstOrDefault(a => a.name?.StartsWith("JASM_") ?? false);

        if (getJasmAsset?.browser_download_url is null)
        {
            Stop(
                "Could not find JASM archive in the newest release on GitHub. This may be due to the developer having to manually upload the zip which can take a few minutes. " +
                "If the problem persists, then you may have to update JASM manually");
            return null;
        }

        release.DownloadUrl = new Uri(getJasmAsset.browser_download_url);
        release.BrowserUrl = new Uri(newestVersionFound?.html_url ?? "https://github.com/Jorixon/JASM/releases");
        release.FileName = getJasmAsset.name ?? "JASM.zip";

        LatestVersion = release.Version.ToString();

        DefaultBrowserUri = release.BrowserUrl;

        return release;
    }

    public void Stop(string stopReason)
    {
        Stopped = true;
        StopReason = stopReason;
    }

    private async Task DownloadLatestVersion(GitHubRelease gitHubRelease, CancellationToken cancellationToken)
    {
        if (Directory.Exists(WorkDir))
        {
            Directory.Delete(WorkDir, true);
        }

        Directory.CreateDirectory(WorkDir);


        _zipPath = Path.Combine(WorkDir, gitHubRelease.FileName);
        if (File.Exists(_zipPath))
        {
            File.Delete(_zipPath);
        }

        var httpClient = CreateHttpClient();
        httpClient.DefaultRequestHeaders.Add("Accept", "application/octet-stream");

        Log("Downloading latest version...");
        var result = await httpClient.GetAsync(gitHubRelease.DownloadUrl, HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!result.IsSuccessStatusCode)
        {
            Stop($"Failed to download latest version. Status Code: {result.StatusCode}, Reason: {result.ReasonPhrase}");
            return;
        }

        await using var stream = await result.Content.ReadAsStreamAsync(cancellationToken);

        await using var fileStream = File.Create(_zipPath);
        await stream.CopyToAsync(fileStream, cancellationToken);
        Log($"Latest version downloaded from {gitHubRelease.DownloadUrl}");
    }

    private async Task UnzipLatestVersion(CancellationToken cancellationToken)
    {
        var process = new Process
        {
            StartInfo =
            {
                FileName = _7zPath,
                Arguments = $"x \"{_zipPath}\" -o\"{WorkDir}\" -y",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        process.Start();
        Log("Extracting downloaded zip file...");
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            Stop($"Failed to extract downloaded zip file. Exit Code: {process.ExitCode}");
            return;
        }


        _extractedJasmFolder = new DirectoryInfo(WorkDir).EnumerateDirectories().FirstOrDefault(folder =>
            folder.Name.StartsWith("JASM", StringComparison.CurrentCultureIgnoreCase))!;

        if (_extractedJasmFolder is null)
        {
            Stop("Failed to find JASM folder in extracted zip file");
            return;
        }

        Log($"JASM Application folder extracted successfully. Path: {_extractedJasmFolder.FullName}");
    }

    private async Task InstallLatestVersion()
    {
        _installedJasmFolder = new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory)).Parent!;
        if (_installedJasmFolder is null)
        {
            Stop("Failed to find installed JASM folder in path");
            return;
        }

        const string jasmExe = "JASM - Just Another Skin Manager.exe";
        _newJasmExePath = Path.Combine(_installedJasmFolder.FullName, jasmExe);

        var containsJasmExe = false;
        var containsSystemFiles = false;
        var systemFileFound = string.Empty;
        var warningFiles = new List<string>();

        foreach (var fileSystemInfo in _installedJasmFolder.EnumerateFileSystemInfos())
        {
            if (fileSystemInfo.Name.Equals(jasmExe,
                    StringComparison.CurrentCultureIgnoreCase))
            {
                containsJasmExe = true;
            }

            if (SystemEntries.WindowsEntries.Any(fileEntry => fileSystemInfo.Name.Equals(fileEntry,
                    StringComparison.CurrentCultureIgnoreCase)))
            {
                containsSystemFiles = true;
                systemFileFound = fileSystemInfo.Name;
            }

            if (fileSystemInfo.Name.Equals("3DMigoto Loader.exe", StringComparison.CurrentCultureIgnoreCase))
            {
                warningFiles.Add(fileSystemInfo.Name);
            }

            if (fileSystemInfo.Name.Equals("3dmigoto", StringComparison.CurrentCultureIgnoreCase))
            {
                warningFiles.Add(fileSystemInfo.Name);
            }

            if (fileSystemInfo.Name.Equals("Mods", StringComparison.CurrentCultureIgnoreCase))
            {
                warningFiles.Add("Mods");
            }
        }

        if (!containsJasmExe)
        {
            Stop(
                $"Failed to find '{jasmExe}' in installed JASM folder. Path: {_installedJasmFolder}");
            return;
        }

        if (containsSystemFiles)
        {
            Stop(
                $"JASM folder seems to contain windows system files, this should never happen. File Found: '{systemFileFound}' at " +
                $"Path: {_installedJasmFolder}");
            return;
        }

        var result = await ShowDeleteWarning(warningFiles);

        if (result is ContentDialogResult.Secondary or ContentDialogResult.None)
        {
            Stop("User cancelled");
            return;
        }

        var autoUpdaterFolder = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);

        Log("Deleting old files...", $"Path: {_installedJasmFolder.FullName}");

        foreach (var fileSystemInfo in _installedJasmFolder.EnumerateFileSystemInfos())
        {
            if (fileSystemInfo.Name.StartsWith(autoUpdaterFolder.Name,
                    StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }

            if (fileSystemInfo.Name.Equals("JASM - Just Another Skin Manager.exe.WebView2",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (fileSystemInfo.Name.Equals("logs",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (fileSystemInfo is DirectoryInfo directoryInfo)
                directoryInfo.Delete(true);
            else
                fileSystemInfo.Delete();
        }

        Log("Copying new files...", $"Path: {_installedJasmFolder.FullName}");

        await Task.Run(() => { CopyFilesRecursively(_extractedJasmFolder, _installedJasmFolder); });

        Log("JASM updated successfully");
    }

    // https://stackoverflow.com/questions/58744/copy-the-entire-contents-of-a-directory-in-c-sharp
    private static void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target)
    {
        foreach (var dir in source.GetDirectories())
            CopyFilesRecursively(dir, target.CreateSubdirectory(dir.Name));
        foreach (var file in source.GetFiles())
            file.CopyTo(Path.Combine(target.FullName, file.Name));
    }

    private void CleanUp()
    {
        Log("Cleaning up work dir...", WorkDir);
        if (Directory.Exists(WorkDir))
        {
            Directory.Delete(WorkDir, true);
        }

        Log("Clean up finished");
    }

    // Copied from GIMI-ModManager.WinUI/Services/UpdateChecker.cs
    private const string ReleasesApiUrl = "https://api.github.com/repos/Jorixon/JASM/releases?per_page=2";

    private async Task<ApiGitHubRelease?> GetLatestVersionAsync(CancellationToken cancellationToken)
    {
        Serilog.Log.Information("Checking for latest version...");

        using var httpClient = CreateHttpClient();

        var result = await httpClient.GetAsync(ReleasesApiUrl, cancellationToken);
        if (!result.IsSuccessStatusCode)
        {
            return null;
        }

        var text = await result.Content.ReadAsStringAsync(cancellationToken);

        var gitHubReleases =
            (JsonConvert.DeserializeObject<ApiGitHubRelease[]>(text)) ?? Array.Empty<ApiGitHubRelease>();

        var latestReleases = gitHubReleases.Where(r => !r.prerelease);
        var latestVersion = latestReleases.OrderByDescending(r => new Version(r.tag_name?.Trim('v') ?? ""));

        return latestVersion.FirstOrDefault();
    }

    private HttpClient CreateHttpClient()
    {
        var httpClient = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 2
        });
        httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        httpClient.DefaultRequestHeaders.Add("User-Agent", "JASM-Just_Another_Skin_Manager-Update-Checker");
        return httpClient;
    }

    private class ApiGitHubRelease
    {
        public string? html_url;
        public string? target_commitish;
        public string? browser_download_url;
        public string? tag_name;
        public bool prerelease;
        public DateTime published_at = DateTime.MinValue;

        public ApiAssets[]? assets;
    }

    private class GitHubRelease
    {
        public Version Version;
        public bool PreRelease;
        public DateTime PublishedAt = DateTime.MinValue;

        public Uri BrowserUrl = null!;
        public Uri DownloadUrl = null!;
        public string FileName = null!;
    }

    private void Log(string logMessage, string? footer = null)
    {
        Serilog.Log.Information("Install Step {StepIndex} | Msg: {Message} | footer: {Footer}", (ProgressLog.Count + 1),
            logMessage, footer);

        var logEntry = new LogEntry
        {
            Message = logMessage,
            Footer = footer,
            TimeStamp = DateTime.Now
        };
        ProgressLog.Insert(0, logEntry);
    }

    [RelayCommand]
    private async Task StartJasm()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = _newJasmExePath,
            UseShellExecute = true
        });
        await Task.Delay(500);
        Application.Current.Exit();
    }


    private async Task<ContentDialogResult> ShowDeleteWarning(ICollection<string> warningFiles)
    {
        var content = new ContentDialog
        {
            Title = "Warning",
            PrimaryButtonText = "Continue",
            DefaultButton = ContentDialogButton.Primary,
            SecondaryButtonText = "Cancel",
            XamlRoot = App.MainWindow.Content.XamlRoot
        };

        var stackPanel = new StackPanel();

        stackPanel.Children.Add(new TextBlock
        {
            Text =
                "All files/folders in the installed JASM folder will be deleted permanently!\n" +
                "This excludes the update folder itself. This action cannot be undone.\n" +
                $"JASM Directory: {_installedJasmFolder.FullName}",
            TextWrapping = TextWrapping.WrapWholeWords,
            IsTextSelectionEnabled = true,
            Margin = new Thickness(0, 0, 0, 10)
        });

        if (warningFiles.Any())
            stackPanel.Children.Add(new TextBlock
            {
                Text = "These files/folders do not belong to JASM and will be deleted as well:\n" +
                       string.Join("\n", warningFiles),
                IsTextSelectionEnabled = true,
                TextWrapping = TextWrapping.WrapWholeWords,
                Margin = new Thickness(0, 0, 0, 10)
            });

        stackPanel.Children.Add(new Button()
        {
            Content = "Open installed JASM folder...",
            Margin = new Thickness(0, 8, 0, 8),
            Command = new AsyncRelayCommand(async () =>
            {
                await Launcher.LaunchFolderAsync(
                    await StorageFolder.GetFolderFromPathAsync(_installedJasmFolder.FullName));
            })
        });

        content.Content = stackPanel;

        return await content.ShowAsync();
    }
}

internal class ApiAssets
{
    public string? name;
    public string? browser_download_url;
}

public class LogEntry
{
    public string Message { get; set; } = string.Empty;
    public string? Footer { get; set; }
    public DateTime TimeStamp { get; set; } = DateTime.Now;
}

public partial class UpdateProgress : ObservableObject
{
    [ObservableProperty] private bool _checkingForLatestUpdate = false;

    [ObservableProperty] private bool _downloadingLatestUpdate = false;

    [ObservableProperty] private bool _extractingLatestUpdate = false;

    [ObservableProperty] private bool _installingLatestUpdate = false;

    public void Reset()
    {
        CheckingForLatestUpdate = false;
        DownloadingLatestUpdate = false;
        ExtractingLatestUpdate = false;
        InstallingLatestUpdate = false;
    }

    public void NextStage()
    {
        if (!CheckingForLatestUpdate)
        {
            CheckingForLatestUpdate = true;
        }
        else if (!DownloadingLatestUpdate)
        {
            DownloadingLatestUpdate = true;
        }
        else if (!ExtractingLatestUpdate)
        {
            ExtractingLatestUpdate = true;
        }
        else if (!InstallingLatestUpdate)
        {
            InstallingLatestUpdate = true;
        }
    }
}