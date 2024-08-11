using Windows.Storage.Pickers;
using Windows.Win32;
using Windows.Win32.Foundation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.Core.Services.CommandService;
using GIMI_ModManager.WinUI.Services.AppManagement;
using GIMI_ModManager.WinUI.Services.Notifications;
using GIMI_ModManager.WinUI.Views.Settings;
using Serilog;

namespace GIMI_ModManager.WinUI.ViewModels.SettingsViewModels;

public partial class CreateCommandViewModel : ObservableObject
{
    private readonly ILogger _logger;
    private readonly CommandService _commandService;
    private readonly NotificationManager _notificationManager;
    private readonly SelectedGameService _selectedGameService;

    private CreateCommandOptions? _createOptions;

    public event EventHandler? CloseRequested;
    public bool IsEditingCommand => _createOptions?.IsEditingCommand == true;

    public CreateCommandViewModel(ILogger logger, CommandService commandService,
        NotificationManager notificationManager, SelectedGameService selectedGameService)
    {
        _commandService = commandService;
        _notificationManager = notificationManager;
        _selectedGameService = selectedGameService;
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

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommandCommand))]
    private bool _isValidCommand;

    [ObservableProperty] private string _commandPreview = string.Empty;


    public async Task Initialize(CreateCommandOptions? options = null)
    {
        if (options is null)
            return;

        _createOptions = options;

        if (options.IsEditingCommand)
        {
            var command = options.CommandDefinition;
            CommandDisplayName = command.CommandDisplayName;
            Command = command.ExecutionOptions.Command;
            Arguments = command.ExecutionOptions.Arguments ?? string.Empty;
            WorkingDirectory = command.ExecutionOptions.WorkingDirectory ?? string.Empty;
            RunAsAdmin = command.ExecutionOptions.RunAsAdmin;
            UseShellExecute = command.ExecutionOptions.UseShellExecute;
            CreateWindow = command.ExecutionOptions.CreateWindow;
            KillProcessOnMainAppExit = command.KillOnMainAppExit;
        }
        else if (options.GameStartCommand)
        {
            CommandDisplayName = $"Start {await _selectedGameService.GetSelectedGameAsync()}";
        }
        else if (options.GameModelImporterCommand)
        {
            CommandDisplayName = "Start 3Dmigoto";
        }
    }


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
        var jasmWorkingDirectory = App.ROOT_DIR;
        string? workingDirectory = null;

        if (!IsValidWorkingDirectory())
        {
            EffectiveWorkingDirectory = prefix + "Invalid working directory";
            return null;
        }

        if (Directory.Exists(WorkingDirectory))
        {
            workingDirectory = WorkingDirectory;
            EffectiveWorkingDirectory = prefix + workingDirectory;
        }
        else if (WorkingDirectory == SpecialVariables.TargetPath)
        {
            workingDirectory = SpecialVariables.TargetPath;
            EffectiveWorkingDirectory = prefix + SpecialVariables.TargetPath;
        }
        else
        {
            workingDirectory = null;
            EffectiveWorkingDirectory = prefix + jasmWorkingDirectory;
        }

        return workingDirectory;
    }

    private void ValidateCommand()
    {
        IsValidCommand = false;
        CommandPreview = string.Empty;

        if (Extensions.IsNullOrEmpty(CommandDisplayName) ||
            Extensions.IsNullOrEmpty(Command) ||
            (!IsExeFoundInPath() && !File.Exists(Command)))
            return;

        if (!IsValidWorkingDirectory())
            return;


        IsValidCommand = true;
        CommandPreview = Command + " " + Arguments;
    }

    private bool IsValidWorkingDirectory()
    {
        if (Extensions.IsNullOrEmpty(WorkingDirectory))
            return true;

        if (WorkingDirectory == SpecialVariables.TargetPath)
            return true;

        return Directory.Exists(WorkingDirectory);
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
        }
        else
        {
            CanToggleCreateWindow = true;
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

        if (file.Name.EndsWith(".py") && Extensions.IsNullOrEmpty(Arguments))
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

        if (Extensions.IsNullOrEmpty(WorkingDirectory) && file.Name.StartsWith("genshin_update_mods"))
        {
            WorkingDirectory = "{{TargetPath}}";
        }

        if (Extensions.IsNullOrEmpty(WorkingDirectory))
            WorkingDirectory = Path.GetDirectoryName(file.Path) ?? string.Empty;

        if (Extensions.IsNullOrEmpty(CommandDisplayName))
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
        try
        {
            await InternalCreateCommand().ConfigureAwait(false);
        }
        catch (Exception e)
        {
            _notificationManager.ShowNotification(
                IsEditingCommand ? "Failed to update command." : "Failed to create command.", e.Message,
                TimeSpan.FromSeconds(5));

            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }


    private async Task InternalCreateCommand()
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


        if (_createOptions?.IsEditingCommand == true)
        {
            await _commandService.UpdateCommandDefinitionAsync(_createOptions.CommandDefinition.Id, createOptions)
                .ConfigureAwait(false);
            CloseRequested?.Invoke(this, EventArgs.Empty);

            _notificationManager.ShowNotification($"Command '{createOptions.CommandDisplayName}' updated successfully.",
                "", TimeSpan.FromSeconds(3));

            await _commandService.SetSpecialCommands(_createOptions.CommandDefinition.Id,
                _createOptions.GameStartCommand, _createOptions.GameModelImporterCommand);
        }
        else
        {
            await _commandService.SaveCommandDefinitionAsync(createOptions).ConfigureAwait(false);
            CloseRequested?.Invoke(this, EventArgs.Empty);

            _notificationManager.ShowNotification($"Command '{createOptions.CommandDisplayName}' created successfully.",
                "",
                TimeSpan.FromSeconds(3));

            if (_createOptions is not null)
                await _commandService.SetSpecialCommands(createOptions.Id, _createOptions.GameStartCommand,
                    _createOptions.GameModelImporterCommand);
        }
    }
}