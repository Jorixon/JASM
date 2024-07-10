using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using GIMI_ModManager.Core.CommandService;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GIMI_ModManager.WinUI.Views;

public sealed partial class CommandProcessViewer : UserControl, IDisposable
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
        };

        Loading += async (_, _) => await ViewModel.InitializeAsync(command, DispatcherQueue).ConfigureAwait(false);
        Loaded += async (_, _) => await ViewModel.StartAsync().ConfigureAwait(false);
    }

    public void Dispose()
    {
        ViewModel.Dispose();
    }
}

public partial class CommandProcessViewerViewModel : ObservableObject, IDisposable
{
    private ProcessCommand? _command;
    private DispatcherQueue? _dispatcherQueue;


    [ObservableProperty] private string _commandDisplayName = string.Empty;

    public ObservableCollection<Inline> OutputTextLines { get; } = new();

    public Task InitializeAsync(ProcessCommand command, DispatcherQueue dispatcherQueue)
    {
        _command = command;
        CommandDisplayName = command.DisplayName;
        _dispatcherQueue = dispatcherQueue;
        return Task.CompletedTask;
    }

    [MemberNotNull(nameof(_command))]
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
    }

    public void Dispose()
    {
        _command?.Dispose();
    }
}

public class OutputEntryVM
{
    public required string Text { get; set; }
    public required string Color { get; set; }
}