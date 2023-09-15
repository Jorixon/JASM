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

}