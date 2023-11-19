using GIMI_ModManager.WinUI.Models;
using GIMI_ModManager.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GIMI_ModManager.WinUI.Views;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class ModsOverviewPage : Page
{
    public ModsOverviewVM ViewModel { get; } = App.GetService<ModsOverviewVM>();


    public ModsOverviewPage()
    {
        InitializeComponent();
    }
}

public class ItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate ModTemplate { get; set; }
    public DataTemplate CharacterTemplate { get; set; }
    public DataTemplate CategoryTemplate { get; set; }

    protected override DataTemplate SelectTemplateCore(object item)
    {
        return item switch
        {
            ModModel => ModTemplate,
            ModdableObjectNode => CharacterTemplate,
            CategoryNode => CategoryTemplate,
            _ => base.SelectTemplateCore(item)
        };
    }
}