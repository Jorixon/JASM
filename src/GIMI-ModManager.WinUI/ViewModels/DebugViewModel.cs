using System.Collections.ObjectModel;
using Windows.Storage.Pickers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Contracts.Services;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.Core.Services;
using GIMI_ModManager.WinUI.Contracts.ViewModels;
using GIMI_ModManager.WinUI.Services;
using GIMI_ModManager.WinUI.Services.ModHandling;
using Serilog;

namespace GIMI_ModManager.WinUI.ViewModels;

public partial class DebugViewModel : ObservableRecipient, INavigationAware
{
    private readonly ILogger _logger;
    private readonly NotificationManager _notificationManager;
    private readonly ISkinManagerService _skinManagerService;
    private readonly ModCrawlerService _modCrawlerService;
    private readonly IModUpdateChecker _modUpdateChecker;
    private readonly ModUpdateAvailableChecker _modUpdateAvailableChecker;


    public DebugViewModel(ILogger logger, NotificationManager notificationManager,
        ISkinManagerService skinManagerService, ModCrawlerService modCrawlerService,
        IModUpdateChecker modUpdateChecker, ModUpdateAvailableChecker modUpdateAvailableChecker)
    {
        _logger = logger;
        _notificationManager = notificationManager;
        _skinManagerService = skinManagerService;
        _modCrawlerService = modCrawlerService;
        _modUpdateChecker = modUpdateChecker;
        _modUpdateAvailableChecker = modUpdateAvailableChecker;
    }

    public void OnNavigatedTo(object parameter)
    {
    }

    public void OnNavigatedFrom()
    {
    }


    [RelayCommand]
    private async Task SelectModDebugAsync()
    {
        var folderPicker = new FolderPicker();
        folderPicker.FileTypeFilter.Add("*");
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

        var folder = await folderPicker.PickSingleFolderAsync();

        if (folder is null)
        {
            return;
        }

        var dir = new DirectoryInfo(folder.Path);
        _modInstallation = ModInstallation.Start(dir);
        RootFolder.Clear();
        RootFolder.Add(new RootFolder(dir));
    }


    private ModInstallation? _modInstallation;

    [ObservableProperty] ObservableCollection<RootFolder> _rootFolder = new();
    [ObservableProperty] private string _customName = string.Empty;
    [ObservableProperty] private string _modUrl = string.Empty;
    [ObservableProperty] private string _author = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
}

public partial class RootFolder : ObservableObject
{
    private readonly DirectoryInfo _folder;
    public string Name => _folder.Name;

    public RootFolder(DirectoryInfo folder)
    {
        _folder = folder;
        _folder.EnumerateFileSystemInfos().ForEach(fse => FileSystemItems.Add(new FileSystemItem(fse)));
    }

    [ObservableProperty] private ObservableCollection<FileSystemItem> _fileSystemItems = new();
}

public partial class FileSystemItem : ObservableObject
{
    private readonly FileSystemInfo _fileSystemInfo;
    private readonly int _recursionCount;
    public string Name => _fileSystemInfo.Name;

    public bool IsFolder => _fileSystemInfo is DirectoryInfo;

    public bool IsFile => _fileSystemInfo is FileInfo;

    public FileSystemItem(FileSystemInfo fileSystemInfo, int recursionCount = 0)
    {
        _fileSystemInfo = fileSystemInfo;
        _recursionCount = recursionCount;

        if (_recursionCount < 2)
        {
            _isExpanded = true;
        }

        if (_recursionCount > 5)
        {
            return;
        }

        if (fileSystemInfo is DirectoryInfo dir)
        {
            _leftIcon = "\uE8B7";
            dir.EnumerateFileSystemInfos()
                .ForEach(fse => FileSystemItems.Add(new FileSystemItem(fse, _recursionCount + 1)));
        }
    }

    [ObservableProperty] private bool _isExpanded;

    [ObservableProperty] private ObservableCollection<FileSystemItem> _fileSystemItems = new();


    [ObservableProperty] private string? _leftIcon;
    [ObservableProperty] private string? _rightIcon;
}

//public class FolderVM : ObservableObject
//{
//    private readonly DirectoryInfo _folder;
//    public string Name => _folder.Name;

//    public FolderVM(DirectoryInfo folder)
//    {
//        _folder = folder;
//    }

//    ObservableCollection<FolderVM> _folders = new();
//    ObservableCollection<FileVM> _files = new();
//}

//public class FileVM : ObservableObject
//{
//    private readonly FileInfo _file;
//    public string Name => _file.Name;

//    public FileVM(FileInfo file)
//    {
//        _file = file;
//    }
//}