// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

using GIMI_ModManager.WinUI.ViewModels;

namespace GIMI_ModManager.WinUI.Views;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class ModUpdateAvailableWindow : WindowEx
{
    public readonly ModUpdateVM ViewModel;

    public ModUpdateAvailableWindow(Guid modId)
    {
        ViewModel = new ModUpdateVM(modId);
        InitializeComponent();
    }
}