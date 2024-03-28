using GIMI_ModManager.WinUI.Models;
using GIMI_ModManager.WinUI.ViewModels;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GIMI_ModManager.WinUI.Views;

public sealed partial class ModSelector : Page, IDisposable
{
    public event EventHandler? CloseRequested;

    public ModSelectorViewModel ViewModel { get; } = App.GetService<ModSelectorViewModel>();

    private CancellationTokenSource _cancellationTokenSource = new();

    private readonly TaskCompletionSource<SelectionResult?> _taskCompletionSource = new();

    private ModSelector(InitOptions options)
    {
        InitializeComponent();

        Loading += (_, _) =>
        {
            ViewModel.InitializeAsync(options, DispatcherQueue, _taskCompletionSource,
                cancellationToken: _cancellationTokenSource.Token);
        };
        ViewModel.CloseRequested += (_, _) => { CloseRequested?.Invoke(this, EventArgs.Empty); };
    }


    public static (ModSelector, Task<SelectionResult?>) Create(InitOptions options)
    {
        var modSelector = new ModSelector(options);
        return (modSelector, modSelector._taskCompletionSource.Task);
    }

    public void Dispose()
    {
        CancellationTokenSource? cts = _cancellationTokenSource;
        _cancellationTokenSource = null!;
        if (cts is null) return;
        cts.Cancel();
        cts.Dispose();
        if (!_taskCompletionSource.Task.IsCompleted)
            _taskCompletionSource.SetResult(null);
    }

    private void GridView_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        foreach (var modModel in e.AddedItems.OfType<ModModel>())
        {
            ViewModel.SelectedMods.Add(modModel);
        }

        foreach (var modModel in e.RemovedItems.OfType<ModModel>())
        {
            ViewModel.SelectedMods.Remove(modModel);
        }
    }

    private void TextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox) return;

        ViewModel.SearchTextChanged(textBox.Text);
    }
}