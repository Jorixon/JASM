using GIMI_ModManager.WinUI.ViewModels;
using GIMI_ModManager.WinUI.Views.Controls;
using Microsoft.Graphics.Display;
using Microsoft.UI.Xaml.Controls;

namespace GIMI_ModManager.WinUI.Views;

public sealed partial class StartupPage : Page
{
    public StartupViewModel ViewModel { get; }

    public StartupPage()
    {
        ViewModel = App.GetService<StartupViewModel>();
        InitializeComponent();
    }

    private void GimiFolder_OnPathChangedEvent(object? sender, FolderSelector.StringEventArgs e)
        => ViewModel.PathToGIMIFolderPicker.Validate(e.Value);


    private void ModsFolder_OnPathChangedEvent(object? sender, FolderSelector.StringEventArgs e)
        => ViewModel.PathToModsFolderPicker.Validate(e.Value);
}