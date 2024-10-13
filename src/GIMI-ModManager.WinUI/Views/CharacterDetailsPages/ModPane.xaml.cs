using Windows.ApplicationModel.DataTransfer;
using GIMI_ModManager.WinUI.ViewModels.CharacterDetailsViewModels.SubViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GIMI_ModManager.WinUI.Views.CharacterDetailsPages;

public sealed partial class ModPane : UserControl
{
    public ModPane()
    {
        InitializeComponent();
    }


    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(ModPaneVM), typeof(ModPane), new PropertyMetadata(default(ModPaneVM)));

    public ModPaneVM ViewModel
    {
        get { return (ModPaneVM)GetValue(ViewModelProperty); }
        set
        {
            SetValue(ViewModelProperty, value);
            OnViewModelSetHandler(ViewModel);
        }
    }


    private void OnViewModelSetHandler(ModPaneVM viewModel)
    {
    }

    private void PaneImage_OnDragEnter(object sender, DragEventArgs e)
    {
        if (ViewModel.IsReadOnly)
            return;
        e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private async void PaneImage_OnDragOver(object sender, DragEventArgs e)
    {
        if (ViewModel.IsReadOnly || ViewModel.BusySetter.IsHardBusy)
            return;

        if (e.DataView.Contains(StandardDataFormats.Uri))
        {
            var url = await e.DataView.GetUriAsync();
            var isValidHttpLink = ViewModel.CanSetImageFromDragDropWeb(url);
            if (isValidHttpLink)
                e.AcceptedOperation = DataPackageOperation.Copy;
        }
        else if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var data = await e.DataView.GetStorageItemsAsync();
            if (ViewModel.CanSetImageFromDragDropStorageItem(data))
                e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }

    private async void PaneImage_OnDrop(object sender, DragEventArgs e)
    {
        if (ViewModel.IsReadOnly || ViewModel.BusySetter.IsHardBusy)
            return;

        var deferral = e.GetDeferral();
        if (e.DataView.Contains(StandardDataFormats.Uri))
        {
            var uri = await e.DataView.GetUriAsync();
            await ViewModel.SetImageFromDragDropWeb(uri);
        }
        else if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            await ViewModel.SetImageFromDragDropFile(await e.DataView.GetStorageItemsAsync());
        }

        deferral.Complete();
    }
}