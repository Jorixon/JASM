using GIMI_ModManager.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GIMI_ModManager.WinUI.Views;

public sealed partial class DebugPage : Page
{
    public DebugViewModel ViewModel { get; } = App.GetService<DebugViewModel>();

    public DebugPage()
    {
        InitializeComponent();
    }

    private async void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        await DuplicateModDialog.ShowAsync();
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