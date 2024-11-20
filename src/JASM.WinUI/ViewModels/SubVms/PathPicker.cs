using System.Collections.ObjectModel;
using Windows.Storage.Pickers;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentValidation;
using Microsoft.UI.Xaml.Controls;

namespace GIMI_ModManager.WinUI.ViewModels.SubVms;

public partial class PathPicker : ObservableRecipient
{
    [ObservableProperty] private string? _path = null;

    public bool PathHasValue => !string.IsNullOrWhiteSpace(Path);

    [ObservableProperty] private bool _isValid;

    public readonly ObservableCollection<InfoMessage> ValidationMessages = new();

    private readonly List<AbstractValidator<PathPicker>> _validators = new();

    public ReadOnlyCollection<AbstractValidator<PathPicker>> Validators => _validators.AsReadOnly();

    public event EventHandler? IsValidChanged;

    private void RaiseIsValidChanged()
    {
        IsValidChanged?.Invoke(this, EventArgs.Empty);
    }

    public PathPicker(params AbstractValidator<PathPicker>[] validators)
    {
        _validators.AddRange(validators);
    }

    public PathPicker(IEnumerable<AbstractValidator<PathPicker>> validators)
    {
        _validators.AddRange(validators);
    }

    public void SetValidators(IEnumerable<AbstractValidator<PathPicker>> validators)
    {
        _validators.Clear();
        _validators.AddRange(validators);
        Validate();
    }


    public void Validate(string? pathToSett = null)
    {
        if (pathToSett is not null)
            Path = pathToSett;

        if (Path is null || string.IsNullOrWhiteSpace(pathToSett))
        {
            ValidationMessages.Clear();
            return;
        }

        ValidationMessages.Clear();
        foreach (var validator in _validators)
        {
            var result = validator.Validate(this);
            if (!result.IsValid)
            {
                result.Errors.ForEach(error =>
                {
                    var severity = error.Severity switch
                    {
                        Severity.Warning => InfoBarSeverity.Warning,
                        Severity.Info => InfoBarSeverity.Informational,
                        _ => InfoBarSeverity.Error
                    };
                    ValidationMessages.Add(new InfoMessage(error.ErrorMessage, severity));
                });
            }
        }

        var oldIsValid = IsValid;
        IsValid = ValidationMessages.All(message => message.Severity != InfoBarSeverity.Error);
        if (oldIsValid != IsValid)
            RaiseIsValidChanged();
    }

    public async Task BrowseFolderPathAsync(WindowEx window)
    {
        var folderPicker = new FolderPicker();
        folderPicker.FileTypeFilter.Add("*");
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

        var folder = await folderPicker.PickSingleFolderAsync();
        Path = folder?.Path;
    }

    public async Task BrowseFilePathAsync(WindowEx window, string extensionFilter = "*")
    {
        var filePicker = new FileOpenPicker();
        filePicker.FileTypeFilter.Add(extensionFilter);
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

        var file = await filePicker.PickSingleFileAsync();
        Path = file?.Path;
    }
}

public readonly struct InfoMessage
{
    public InfoMessage(string message, InfoBarSeverity severity = InfoBarSeverity.Informational)
    {
        Message = message ?? string.Empty;
        Severity = severity;
    }

    public string Message { get; }
    public InfoBarSeverity Severity { get; }
}