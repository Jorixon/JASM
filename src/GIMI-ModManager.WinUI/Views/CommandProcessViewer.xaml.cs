using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Windows.System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Services.CommandService;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GIMI_ModManager.WinUI.Views;

public sealed partial class CommandProcessViewer : UserControl
{
    public CommandProcessViewerViewModel ViewModel { get; } = App.GetService<CommandProcessViewerViewModel>();

    public CommandProcessViewer(ProcessCommand command)
    {
        InitializeComponent();

        ViewModel.OutputTextLines.CollectionChanged += (_, e) =>
        {
            foreach (var eNewItem in e.NewItems?.OfType<Inline>() ?? [])
            {
                OutputTextBlock.Inlines.Add(eNewItem);
            }

            if (ViewModel.IsAutoScroll)
            {
                OutputScrollViewer.ScrollToVerticalOffset(OutputScrollViewer.ScrollableHeight);
            }
        };

        Loading += async (_, _) => await ViewModel.InitializeAsync(command, DispatcherQueue).ConfigureAwait(false);
        Loaded += async (_, _) => await ViewModel.StartAsync().ConfigureAwait(false);
    }

    private void InputTextBox_OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter || !ViewModel.WriteInputCommand.CanExecute(InputTextBox.Text)) return;

        ViewModel.WriteInputCommand.ExecuteAsync(InputTextBox.Text);
    }
}

public partial class CommandProcessViewerViewModel : ObservableObject
{
    private ProcessCommand? _command;
    private DispatcherQueue? _dispatcherQueue;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(KillProcessCommand), nameof(WriteInputCommand))]
    private bool _isRunning;

    [ObservableProperty] private string _commandDisplayName = string.Empty;

    [ObservableProperty] private bool _isAutoScroll = true;

    [ObservableProperty] private string? _inputText;

    public ObservableCollection<Inline> OutputTextLines { get; } = new();

    public Task InitializeAsync(ProcessCommand command, DispatcherQueue dispatcherQueue)
    {
        _command = command;
        CommandDisplayName = command.DisplayName;
        _dispatcherQueue = dispatcherQueue;
        return Task.CompletedTask;
    }

    [MemberNotNull(nameof(_command), nameof(_dispatcherQueue))]
    private void EnsureInitialized()
    {
        if (_command is null)
            throw new InvalidOperationException("ViewModel not initialized");

        if (_dispatcherQueue is null)
            throw new InvalidOperationException("ViewModel not initialized");
    }


    private void OnProcessExit()
    {
        _dispatcherQueue!.TryEnqueue(() =>
        {
            var exitCode = _command!.ExitCode;
            IsRunning = _command!.IsRunning;

            OutputTextLines.Add(new Run()
            {
                Text = $"Process exited with exit code: {exitCode}" + Environment.NewLine
            });
        });
    }

    public async Task StartAsync()
    {
        EnsureInitialized();

        await Task.Delay(500);

        _command.OutputDataReceived += (_, args) =>
        {
            _dispatcherQueue!.TryEnqueue(() =>
            {
                OutputTextLines.Add(new Run()
                {
                    Text = (args.Data ?? string.Empty) + Environment.NewLine
                });
            });
        };

        _command.ErrorDataReceived += (_, args) =>
        {
            _dispatcherQueue!.TryEnqueue(() =>
            {
                OutputTextLines.Add(new Run()
                {
                    Text = (args.Data ?? string.Empty) + Environment.NewLine,
                    Foreground = new SolidColorBrush(Colors.Red)
                });
            });
        };

        _command.Exited += (_, _) => { OnProcessExit(); };

        _command.Start();
        IsRunning = _command.IsRunning;
    }


    [RelayCommand(CanExecute = nameof(IsRunning))]
    private async Task KillProcessAsync()
    {
        EnsureInitialized();


        await _command.KillAsync().ConfigureAwait(false);
    }

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private async Task WriteInputAsync(string? input)
    {
        EnsureInitialized();

        await _command.WriteInputAsync(input);
        InputText = string.Empty;
        OutputTextLines.Add(new Run()
        {
            Text = ">" + input + Environment.NewLine
        });
    }
}