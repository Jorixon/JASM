using CommunityToolkit.WinUI.UI.Controls;
using GIMI_ModManager.WinUI.ViewModels.CharacterDetailsViewModels.SubViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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
        }
        catch (Exception)
        {
            // ignored
        }
    }
}