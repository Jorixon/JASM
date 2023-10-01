using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;

namespace JASM.AutoUpdater;

public partial class MainPageVM : ObservableRecipient
{
    private readonly string WorkDir = Path.Combine(Path.GetTempPath(), "JASM_Auto_Updater");
    private string _zipPath = string.Empty;

    private readonly string _7zPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Assets\7z\", "7z.exe");

    [ObservableProperty] private bool _inStartupView = true;

    [ObservableProperty] private bool _updateProcessStarted = false;

    public ObservableCollection<string> Log { get; } = new();

    public UpdateProgress UpdateProgress { get; } = new();

    public Version? InstalledVersion { get; set; } = new(0, 0, 0, 0);

    public CancellationTokenSource CancellationTokenSource { get; } = new();


    [ObservableProperty] private bool _stopped;
    [ObservableProperty] private string? _stopReason;

    public MainPageVM()
    {
    }

    [RelayCommand]
    private async Task StartUpdate()
    {
        InStartupView = false;
        UpdateProcessStarted = true;


        var release = await IsNewerVersionAvailable();
        UpdateProgress.NextStage();
        if (Stopped || release is null)
            return;


        await DownloadLatestVersion(release);
        UpdateProgress.NextStage();

        if (Stopped)
        {
            CleanUp();
            return;
        }

        await UnzipLatestVersion();
        UpdateProgress.NextStage();

        if (Stopped)
        {
            CleanUp();
            return;
        }

        await InstallLatestVersion();
        UpdateProgress.NextStage();

        CleanUp();
    }


    private async Task<GitHubRelease?> IsNewerVersionAvailable()
    {
        var newestVersionFound = await GetLatestVersionAsync(CancellationTokenSource.Token);

        Log.Add($"Newest version found: {newestVersionFound?.tag_name}");

        var release = new GitHubRelease()
        {
            Version = new Version(newestVersionFound?.tag_name?.Trim('v') ?? ""),
            PreRelease = newestVersionFound?.prerelease ?? false,
            PublishedAt = newestVersionFound?.published_at ?? DateTime.MinValue
        };

        if (release.Version <= InstalledVersion)
        {
            Stop("Installed version is newer than or equal to the newest version");
            return null;
        }

        var getJasmAsset = newestVersionFound?.assets?.FirstOrDefault(a => a.name?.StartsWith("JASM_") ?? false);

        if (getJasmAsset == null && getJasmAsset?.browser_download_url is null)
        {
            Stop("No JASM asset found");
            return null;
        }

        release.DownloadUrl = new Uri(getJasmAsset.browser_download_url!);
        release.FileName = getJasmAsset.name ?? "JASM.zip";

        return release;
    }

    private void Stop(string stopReason)
    {
        Stopped = true;
        StopReason = stopReason;
    }

    private async Task DownloadLatestVersion(GitHubRelease gitHubRelease)
    {
        //if (Directory.Exists(WorkDir))
        //{
        //    Directory.Delete(WorkDir, true);
        //}

        Directory.CreateDirectory(WorkDir);


        _zipPath = Path.Combine(WorkDir, gitHubRelease.FileName);
        //if (File.Exists(_zipPath))
        //{
        //    File.Delete(_zipPath);
        //}

        //File.Create(_zipPath).Close();
        //return;

        //var httpClient = CreateHttpClient();
        //httpClient.DefaultRequestHeaders.Add("Accept", "application/octet-stream");

        //var result = await httpClient.GetAsync(gitHubRelease.DownloadUrl, HttpCompletionOption.ResponseHeadersRead,
        //    CancellationTokenSource.Token);

        //if (!result.IsSuccessStatusCode)
        //{
        //    Stop($"Failed to download latest version. Status Code: {result.StatusCode}, Reason: {result.ReasonPhrase}");
        //    return;
        //}

        //await using var stream = await result.Content.ReadAsStreamAsync();

        //await using var fileStream = File.Create(_zipPath);
        //await stream.CopyToAsync(fileStream, CancellationTokenSource.Token);
    }

    private async Task UnzipLatestVersion()
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
        Log.Add("Extracting downloaded zip file...");
        await process.WaitForExitAsync(CancellationTokenSource.Token);

        if (process.ExitCode != 0)
        {
            Stop($"Failed to extract downloaded zip file. Exit Code: {process.ExitCode}");
            return;
        }


        var jasmFolder = new DirectoryInfo(WorkDir).EnumerateDirectories().FirstOrDefault(folder =>
            folder.Name.StartsWith("JASM", StringComparison.CurrentCultureIgnoreCase));

        if (jasmFolder is null)
        {
            Stop("Failed to find JASM folder in extracted zip file");
            return;
        }

        Log.Add($"JASM Application folder extracted successfully. Path: {jasmFolder.FullName}");
    }

    private async Task InstallLatestVersion()
    {
        var installedJasmFolder = new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory)).Parent;
        if (installedJasmFolder is null)
        {
            Stop("Failed to find installed JASM folder");
            return;
        }

        // Show warning message box before deleting files


        foreach (var FileSystemInfo in installedJasmFolder.EnumerateFileSystemInfos())
        {
        }
    }

    private void CleanUp()
    {
        //if (Directory.Exists(WorkDir))
        //{
        //    Directory.Delete(WorkDir, true);
        //}
    }

    // Copied from GIMI-ModManager.WinUI/Services/UpdateChecker.cs
    private const string ReleasesApiUrl = "https://api.github.com/repos/Jorixon/JASM/releases?per_page=2";

    private async Task<ApiGitHubRelease?> GetLatestVersionAsync(CancellationToken cancellationToken)
    {
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

        public Uri DownloadUrl = null!;
        public string FileName = null!;
    }
}

internal class ApiAssets
{
    public string? name;
    public string? browser_download_url;
}

public partial class UpdateProgress : ObservableObject
{
    [ObservableProperty] private bool _checkingForLatestUpdate = false;

    [ObservableProperty] private bool _downloadingLatestUpdate = false;

    [ObservableProperty] private bool _extractingLatestUpdate = false;

    [ObservableProperty] private bool _installingLatestUpdate = false;

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