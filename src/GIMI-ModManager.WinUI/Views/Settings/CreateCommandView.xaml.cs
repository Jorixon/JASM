using Windows.Storage.Pickers;
using Windows.Win32;
using Windows.Win32.Foundation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.Core.Services.CommandService;
using GIMI_ModManager.WinUI.Services.AppManagement;
using GIMI_ModManager.WinUI.Services.Notifications;
using Microsoft.UI.Xaml.Controls;
using Serilog;

namespace GIMI_ModManager.WinUI.Views.Settings;

public sealed partial class CreateCommandView : UserControl, IClosableElement
{
    public CreateCommandViewModel ViewModel { get; } = App.GetService<CreateCommandViewModel>();

    public event EventHandler? CloseRequested;

    public CreateCommandView()
    {
        InitializeComponent();
        ViewModel.CloseRequested += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}

public partial class CreateCommandViewModel : ObservableObject
{
    private readonly ILogger _logger;
    private readonly CommandService _commandService;
    private readonly NotificationManager _notificationManager;

    public event EventHandler? CloseRequested;

    public CreateCommandViewModel(ILogger logger, CommandService commandService,
        NotificationManager notificationManager)
    {
        _commandService = commandService;
        _notificationManager = notificationManager;
        _logger = logger.ForContext<CreateCommandViewModel>();
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not nameof(EffectiveWorkingDirectory))
            {
                SetEffectiveWorkingDirectory();
            }


            if (e.PropertyName is nameof(CommandPreview) or nameof(IsValidCommand))
                return;
            ValidateCommand();
        };
        SetEffectiveWorkingDirectory();
        ValidateCommand();
    }

    [ObservableProperty] private string _commandDisplayName = string.Empty;

    [ObservableProperty] private string _command = string.Empty;

    [ObservableProperty] private string _arguments = string.Empty;

    [ObservableProperty] private bool _canEditWorkingDirectory = true;

    [ObservableProperty] private string _workingDirectory = string.Empty;

    [ObservableProperty] private string _effectiveWorkingDirectory = string.Empty;

    [ObservableProperty] private bool _runAsAdmin;

    [ObservableProperty] private bool _canToggleUseShellExecute = true;
    [ObservableProperty] private bool _useShellExecute;

    [ObservableProperty] private bool _canToggleCreateWindow = true;

    [ObservableProperty] private bool _createWindow = true;

    [ObservableProperty] private bool _killProcessOnMainAppExit;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(CreateCommandCommand))]
    private bool _isValidCommand;

    [ObservableProperty] private string _commandPreview = string.Empty;


    private unsafe bool IsExeFoundInPath()
    {
        var index = 0;
        var charBuffer = new Span<char>(new char[500]);

        var command = Command.EndsWith(".exe") ? Command : Command + ".exe";

        foreach (var c in command.AsEnumerable().Append('\0'))
        {
            charBuffer[index] = c;
            index++;
        }

        if (charBuffer != null && charBuffer.LastIndexOf('\0') == -1)
            throw new ArgumentException("Required null terminator missing.");

        fixed (char* p = charBuffer)
        {
            var result = PInvoke.PathFindOnPath(new PWSTR(p));

            return result;
        }
    }

    private string? SetEffectiveWorkingDirectory()
    {
        const string prefix = "Effective working directory: ";
        string? workingDirectory = null;

        if (UseShellExecute)
        {
            EffectiveWorkingDirectory = prefix + "Executable location";
        }
        else if (WorkingDirectory == SpecialVariables.TargetPath || Arguments.Contains(SpecialVariables.TargetPath))
        {
            EffectiveWorkingDirectory = prefix + SpecialVariables.TargetPath;
        }
        else if (Directory.Exists(WorkingDirectory))
        {
            workingDirectory = WorkingDirectory;
            EffectiveWorkingDirectory = prefix + workingDirectory;
        }
        else
        {
            workingDirectory = App.ROOT_DIR;
            EffectiveWorkingDirectory = prefix + workingDirectory;
        }

        return workingDirectory;
    }

    private void ValidateCommand()
    {
        IsValidCommand = false;
        CommandPreview = string.Empty;

        if (CommandDisplayName.IsNullOrEmpty() ||
            Command.IsNullOrEmpty() ||
            (!IsExeFoundInPath() && !File.Exists(Command)))
            return;


        IsValidCommand = true;
        CommandPreview = (Command + " " + Arguments).Trim();
    }


    [RelayCommand]
    private void ToggleRunAsAdmin()
    {
        if (RunAsAdmin)
        {
            UseShellExecute = true;
            CanToggleUseShellExecute = false;
            CreateWindow = true;
            CanToggleCreateWindow = false;
            CanEditWorkingDirectory = false;
        }
        else
        {
            CanToggleUseShellExecute = true;
            CanToggleCreateWindow = true;
        }
    }

    [RelayCommand]
    private void ToggleUseShellExecute()
    {
        if (UseShellExecute)
        {
            CreateWindow = true;
            CanToggleCreateWindow = false;
            CanEditWorkingDirectory = false;
        }
        else
        {
            CanToggleCreateWindow = true;
            CanEditWorkingDirectory = true;
        }
    }

    [RelayCommand]
    private async Task SelectExecutableAsync()
    {
        var filePicker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.ComputerFolder,
            FileTypeFilter = { "*", ".exe", ".py" },
            CommitButtonText = "Select"
        };


        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);
        var file = await filePicker.PickSingleFileAsync();
        if (file is null)
        {
            _logger.Debug("User cancelled file picker.");
            return;
        }

        if (file.Name.EndsWith(".py") && Arguments.IsNullOrEmpty())
        {
            Command = "python";
            var arg = $"\"{file.Path}\"";

            // -u flag is used to disable buffering
            // This is needed to get the output of the python script in real-time when redirecting output
            if (!CreateWindow)
                arg = "-u " + arg;

            Arguments = arg;
        }
        else
        {
            Command = file.Path;
        }

        if (WorkingDirectory.IsNullOrEmpty())
            WorkingDirectory = Path.GetDirectoryName(file.Path) ?? string.Empty;

        if (CommandDisplayName.IsNullOrEmpty())
            CommandDisplayName = file.Name;
    }

    [RelayCommand]
    private async Task SelectWorkingDirectoryAsync()
    {
        var folderPicker = new FolderPicker()
        {
            SuggestedStartLocation = PickerLocationId.ComputerFolder,
            CommitButtonText = "Select Folder"
        };


        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
        var folder = await folderPicker.PickSingleFolderAsync();

        if (folder is null)
        {
            _logger.Debug("User cancelled folder picker.");
            return;
        }

        WorkingDirectory = folder.Path;
    }


    [RelayCommand(CanExecute = nameof(IsValidCommand))]
    private async Task CreateCommandAsync()
    {
        var execOptions = new CommandExecutionOptions()
        {
            UseShellExecute = UseShellExecute,
            RunAsAdmin = RunAsAdmin,
            Command = Command,
            Arguments = Arguments,
            WorkingDirectory = SetEffectiveWorkingDirectory(),
            CreateWindow = CreateWindow
        };

        var createOptions = new CommandDefinition()
        {
            CommandDisplayName = CommandDisplayName,
            KillOnMainAppExit = KillProcessOnMainAppExit,
            ExecutionOptions = execOptions
        };

        await _commandService.SaveCommandDefinitionAsync(createOptions).ConfigureAwait(false);
        CloseRequested?.Invoke(this, EventArgs.Empty);

        _notificationManager.ShowNotification($"Command '{createOptions.CommandDisplayName}' created successfully.", "",
            TimeSpan.FromSeconds(3));
    }
}