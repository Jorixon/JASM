using CommunityToolkit.WinUI.UI.Controls;
using GIMI_ModManager.WinUI.ViewModels.CharacterDetailsViewModels.SubViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GIMI_ModManager.WinUI.Views.CharacterDetailsPages;

public sealed partial class ModGrid : UserControl
{
    public DataGrid DataGrid;

    public ModGrid()
    {
        InitializeComponent();
        Unloaded += OnUnloaded;
        DataGrid = ModListGrid;
    }


    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(ModGridVM), typeof(ModGrid), new PropertyMetadata(default(ModGridVM)));

    public ModGridVM ViewModel
    {
        get { return (ModGridVM)GetValue(ViewModelProperty); }
        set
        {
            SetValue(ViewModelProperty, value);
            OnViewModelSetHandler(ViewModel);
        }
    }

    private void ModListGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SelectionChanged_EventHandler(
            e.AddedItems.OfType<ModRowVM>().ToArray(),
            e.RemovedItems.OfType<ModRowVM>().ToArray());
    }


    private void OnViewModelSetHandler(ModGridVM viewModel)
    {
        viewModel.SelectModEvent += ViewModelOnSelectModEvent;
        viewModel.SortEvent += SetSortUiEventHandler;
    }

    private void ViewModelOnSelectModEvent(object? sender, ModGridVM.SelectModRowEventArgs e)
    {
        DataGrid.SelectedIndex = e.Index;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel.SelectModEvent -= ViewModelOnSelectModEvent;
            ViewModel.SortEvent -= SetSortUiEventHandler;
        }
        catch (Exception)
        {
            // ignored
        }
    }

    private void SetSortUiEventHandler(object? sender, SortEvent sortEvent)
    {
        var column = DataGrid.Columns.FirstOrDefault(c => c.Tag.ToString() == sortEvent.SortColumn);
        if (column is null)
        {
            return;
        }

        column.SortDirection =
            sortEvent.IsDescending ? DataGridSortDirection.Descending : DataGridSortDirection.Ascending;

        // Reset other columns
        // Remove sorting indicators from other columns
        foreach (var dgColumn in ModListGrid.Columns)
            if (dgColumn.Tag.ToString() != sortEvent.SortColumn)
                dgColumn.SortDirection = null;
    }

    private void OnColumnSort(object? sender, DataGridColumnEventArgs e)
    {
        var sortColumn = e.Column.Tag.ToString() ?? "";
        var isDescending = e.Column.SortDirection == null || e.Column.SortDirection == DataGridSortDirection.Ascending;
        e.Column.SortDirection = isDescending ? DataGridSortDirection.Descending : DataGridSortDirection.Ascending;


        ViewModel.SetModSorting(sortColumn, isDescending, true);


        // Reset other columns
        // Remove sorting indicators from other columns
        foreach (var dgColumn in ModListGrid.Columns)
            if (dgColumn.Tag.ToString() != sortColumn)
                dgColumn.SortDirection = null;
    }

    private async void ModListGrid_OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        await ViewModel.OnKeyDown_EventHandlerAsync(e.Key).ConfigureAwait(false);
    }
}