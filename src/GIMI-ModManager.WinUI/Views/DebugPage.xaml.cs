using System.Collections.ObjectModel;
using CommunityToolkit.WinUI.UI.Controls;
using GIMI_ModManager.WinUI.Models;
using GIMI_ModManager.WinUI.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace GIMI_ModManager.WinUI.Views;

public sealed partial class DebugPage : Page
{
    public DebugViewModel ViewModel { get; } = App.GetService<DebugViewModel>();

    public DebugPage()
    {
        this.InitializeComponent();
    }

    /*private async void UIElement_OnDrop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count > 0)
            {
                var folder = items[0] as StorageFolder;
                folder = await folder.GetFolderAsync("Ayaka-ArmoredBikini(NSFW)");
                foreach (var folderr in await folder.GetFoldersAsync())
                {
                    Debug.WriteLine(folderr.Name);
                }

                foreach (var file in await folder.GetFilesAsync())
                {
                    Debug.WriteLine(file.Name);
                    if (file.Name == "merged.ini")
                        TextBlock.Text = await FileIO.ReadTextAsync(file);
                }

            }
        }
    }

    private void UIElement_OnDragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
    }*/
    private void ModListGrid_OnSorting(object? sender, DataGridColumnEventArgs e)
    {
        if (e.Column.Tag.ToString() == "Name")
        {
            //Implement sort on the column "Range" using LINQ
            if (e.Column.SortDirection == null || e.Column.SortDirection == DataGridSortDirection.Descending)
            {
                var sortedMods = from modEntry in ViewModel.ModListVM.BackendMods
                                 orderby modEntry.Name ascending
                    select modEntry;

                ViewModel.ModListVM.ReplaceMods(sortedMods);


                e.Column.SortDirection = DataGridSortDirection.Ascending;
            }
            else
            {
                var sortedMods = from modEntry in ViewModel.ModListVM.BackendMods
                                 orderby modEntry.Name descending
                    select modEntry;

                ViewModel.ModListVM.ReplaceMods(sortedMods);


                e.Column.SortDirection = DataGridSortDirection.Descending;
            }
        }


        if (e.Column.Tag.ToString() == "IsEnabled")
        {
            var enabledMods = ViewModel.ModListVM.BackendMods.Where(modEntry => modEntry.IsEnabled);
            var disabledMods = ViewModel.ModListVM.BackendMods.Where(modEntry => !modEntry.IsEnabled);

            if (e.Column.SortDirection == null || e.Column.SortDirection == DataGridSortDirection.Descending)
            {
                var sortedMods = enabledMods.Concat(disabledMods);

                ViewModel.ModListVM.ReplaceMods(sortedMods);
                e.Column.SortDirection = DataGridSortDirection.Ascending;
            }
            else
            {
                var sortedMods = disabledMods.Concat(enabledMods);

                ViewModel.ModListVM.ReplaceMods(sortedMods);
                e.Column.SortDirection = DataGridSortDirection.Descending;
            }
        }


        // Remove sorting indicators from other columns
        foreach (var dgColumn in ModListGrid.Columns)
        {
            if (dgColumn.Tag.ToString() != e.Column.Tag.ToString())
            {
                dgColumn.SortDirection = null;
            }
        }
    }
}