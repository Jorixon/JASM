using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Services.GameBanana.Models;
using GIMI_ModManager.WinUI.Helpers;

namespace GIMI_ModManager.WinUI.ViewModels.ModPageViewModels;

public partial class ModFileInfoVm : ObservableObject
{
    private readonly ModFileInfo _modFileInfo;

    public string ModId => _modFileInfo.ModId;
    public string FileId => _modFileInfo.FileId;
    public string FileName => _modFileInfo.FileName;

    public DateTime DateAdded => _modFileInfo.DateAdded;

    public string DateAddedTooltipFormat => $"Submitted: {DateAdded}";

    public TimeSpan Age => DateTime.Now - DateAdded;

    public string AgeFormated => FormaterHelpers.FormatTimeSinceAdded(Age);

    public string Description => _modFileInfo.Description;

    public string Md5Hash => _modFileInfo.Md5Checksum;
    [ObservableProperty] private bool _isNew;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand), nameof(InstallCommand))]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand), nameof(InstallCommand))]
    [NotifyPropertyChangedFor(nameof(ShowDownloadButton), nameof(ShowInstallButton))]
    private InstallStatus _status = InstallStatus.NotStarted;

    [ObservableProperty] private int _downloadProgress;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand), nameof(DownloadCommand))]
    private FileInfo? _archiveFile;

    public IProgress<int> Progress { get; }


    public bool ShowDownloadButton => Status is InstallStatus.NotStarted or InstallStatus.Downloading;

    public bool ShowInstallButton =>
        Status is InstallStatus.Downloaded or InstallStatus.Installing or InstallStatus.Installed;

    public ModFileInfoVm(ModFileInfo modFileInfo,
        IAsyncRelayCommand downloadCommand, IAsyncRelayCommand installCommand)
    {
        _modFileInfo = modFileInfo;
        DownloadCommand = downloadCommand;
        InstallCommand = installCommand;
        Progress = new Progress<int>(i => DownloadProgress = i);
    }

    public IAsyncRelayCommand DownloadCommand { get; }
    public IAsyncRelayCommand InstallCommand { get; }


    public enum InstallStatus
    {
        NotStarted,
        Downloading, // Downloading from GameBanana
        Downloaded, // Downloaded from GameBanana
        Installing, // Installing to the game, mod installation window open
        Installed // Installed to the game
    }
}