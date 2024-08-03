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

    private void CommandMenuFlyout_OnOpening(object? sender, object e)
    {
        var flyout = sender as Flyout;
        if (flyout is null)
        {
            return;
        }

        var dataContext = ((sender as Flyout)?.Content as Grid)?.DataContext;

        if (dataContext is ModModel mod)
        {
            ViewModel.TargetPath = mod.FolderPath;
        }
        else if (dataContext is ModdableObjectNode node)
        {
            ViewModel.TargetPath = node.FolderPath;
        }
        else if (dataContext is CategoryNode category)
        {
            ViewModel.TargetPath = category.FolderPath;
        }
        else
            flyout.Hide();

        // Hacky way to show target path in the flyout
        foreach (var viewModelCommandDefinition in ViewModel.CommandDefinitions)
        {
            viewModelCommandDefinition.TargetPath = ViewModel.TargetPath;
        }
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