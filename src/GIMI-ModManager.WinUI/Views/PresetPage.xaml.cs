using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using GIMI_ModManager.WinUI.ViewModels;
using GIMI_ModManager.WinUI.Views.Controls;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace GIMI_ModManager.WinUI.Views;

public sealed partial class PresetPage : Page
{
    public PresetViewModel ViewModel { get; } = App.GetService<PresetViewModel>();

    public PresetPage()
    {
        InitializeComponent();
        PresetsList.DragItemsCompleted += PresetsList_DragItemsCompleted;
    }

    private async void PresetsList_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        if (args.DropResult == DataPackageOperation.Move && ViewModel.ReorderPresetsCommand.CanExecute(null))
        {
            await ViewModel.ReorderPresetsCommand.ExecuteAsync(null);
        }
    }

    private async void UIElement_OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var PresetVm = (ModPresetVm)((EditableTextBlock)sender).DataContext;

        if (e.Key == VirtualKey.Enter && ViewModel.RenamePresetCommand.CanExecute(PresetVm))
        {
            await ViewModel.RenamePresetCommand.ExecuteAsync(PresetVm);
        }
    }
}