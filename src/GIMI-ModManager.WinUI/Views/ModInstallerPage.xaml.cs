using Windows.Storage;
using Windows.System;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace GIMI_ModManager.WinUI.Views;

public sealed partial class ModInstallerPage : Page, IDisposable
{
    public ModInstallerVM ViewModel { get; } = App.GetService<ModInstallerVM>();

    public ModInstallerPage(ICharacterModList characterModList, DirectoryInfo modToInstall)
    {
        InitializeComponent();
        ViewModel.DuplicateModDialog += OnDuplicateModFound;
        ViewModel.InstallerFinished += (_, _) => { DispatcherQueue.TryEnqueue(() => { IsEnabled = false; }); };
        Loading += (_, _) => { ViewModel.InitializeAsync(characterModList, modToInstall, DispatcherQueue); };
    }

    private async void OnDuplicateModFound(object? sender, EventArgs e)
    {
        await DuplicateModDialog.ShowAsync();
    }


    private async void RootFolder_DoubleClicked(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (sender is not TreeViewItem treeView) return;
        if (treeView.DataContext is not RootFolder rootFolder) return;
        if (!Directory.Exists(rootFolder.Path)) return;

        await Launcher.LaunchFolderAsync(await StorageFolder.GetFolderFromPathAsync(rootFolder.Path));
    }

    private void FrameworkElement_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not TreeViewItem treeView) return;
        if (treeView.DataContext is not FileSystemItem fileSystemItem) return;

        if (fileSystemItem.IsFolder)
            treeView.ContextFlyout = FolderFlyout;
        else if (fileSystemItem.IsFile)
            treeView.ContextFlyout = FileFlyout;
        else
            treeView.ContextFlyout = null;
    }

    private async void FileSystemItem_DoubleClicked(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (sender is not Grid grid) return;
        if (grid.DataContext is not FileSystemItem fileSystemItem || fileSystemItem.IsFile) return;
        if (!Directory.Exists(fileSystemItem.Path)) return;

        await Launcher.LaunchFolderAsync(await StorageFolder.GetFolderFromPathAsync(fileSystemItem.Path));
    }

    private void UrlTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            if (sender is not TextBox textBox) return;
            ViewModel.ModUrl = textBox.Text.Trim();
        }
    }

    public void Dispose()
    {
        ViewModel.Dispose();
    }
}

class ExplorerItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate RootFolderTemplate { get; set; }
    public DataTemplate FileSystemItem { get; set; }

    protected override DataTemplate SelectTemplateCore(object item)
    {
        return item switch
        {
            RootFolder => RootFolderTemplate,
            ViewModels.FileSystemItem => FileSystemItem,
            _ => throw new ArgumentOutOfRangeException(nameof(item))
        };
    }
}