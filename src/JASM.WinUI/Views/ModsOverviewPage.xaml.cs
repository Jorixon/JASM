using Windows.Foundation;
using CommunityToolkit.WinUI.UI;
using GIMI_ModManager.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GIMI_ModManager.WinUI.Views;

public sealed partial class ModsOverviewPage : Page
{
    public ModsOverviewVM ViewModel { get; } = App.GetService<ModsOverviewVM>();

    private readonly TaskCompletionSource _pageLoadingTask = new();

    public ModsOverviewPage()
    {
        InitializeComponent();
        Loaded += (sender, args) => { SearchBox.Focus(FocusState.Programmatic); };

        OverviewTreeView.Loaded += OnOverviewTreeLoadedHandler;
    }

    private async void OnOverviewTreeLoadedHandler(object sender, RoutedEventArgs routedEventArgs)
    {
        var treeViewScrollViewer = OverviewTreeView.FindDescendant<ScrollViewer>();

        if (treeViewScrollViewer is not null)
        {
            treeViewScrollViewer.ViewChanged += (o, eventArgs) =>
            {
                ModsOverviewVM.PageState.ScrollPosition = treeViewScrollViewer.VerticalOffset;
            };
        }

        var shouldRestoreState = await ViewModel.ViewModelLoading.Task;
        if (!shouldRestoreState && ViewModel.GoToNode is not null)
        {
            await Task.Delay(500);
            var goToNode = ViewModel.GoToNode;

            var moddableObjectNode = OverviewTreeView.RootNodes
                .SelectMany(n => n.Children)
                .FirstOrDefault(n => n.Content is ModdableObjectNode modNode && modNode.Id == goToNode.Id);

            if (OverviewTreeView.ContainerFromNode(moddableObjectNode) is TreeViewItem goToItem)
            {
                // ChatGPT stuff

                // Calculate the vertical position of the TreeViewItem
                var transform = goToItem.TransformToVisual(OverviewTreeView);
                var position = transform.TransformPoint(new Point(0, 0));

                // Calculate the offset to center the item
                var treeViewHeight = OverviewTreeView.ActualHeight;
                var itemHeight = OverviewTreeView.ActualHeight;
                var offset = position.Y + (itemHeight / 2) - (treeViewHeight / 2);

                // Scroll to the calculated offset
                treeViewScrollViewer?.ChangeView(null, offset, null);
            }

            return;
        }

        SearchBox.Text = ModsOverviewVM.PageState.SearchText;
        await ViewModel.RestoreState(treeViewScrollViewer).ConfigureAwait(false);
        ViewModel.ViewModelLoading.Task.Dispose();
    }

    private void CommandMenuFlyout_OnOpening(object? sender, object e)
    {
        var flyout = sender as Flyout;
        if (flyout is null)
        {
            return;
        }

        var dataContext = ((sender as Flyout)?.Content as Grid)?.DataContext;

        if (dataContext is ModModelNode mod)
        {
            ViewModel.TargetPath = mod.Mod.FolderPath;
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


    private void SearchBox_OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        ViewModel.SearchTextChangedHandler(args.QueryText);
    }

    private void GoToCharacterButton_OnClick(object sender, RoutedEventArgs e)
    {
        var character = ((sender as Button)?.DataContext as ModdableObjectNode)?.ModdableObject;
        if (character is not null && ViewModel.GoToCharacterCommand.CanExecute(character))
        {
            ViewModel.GoToCharacterCommand.Execute(character);
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
            ModModelNode => ModTemplate,
            ModdableObjectNode => CharacterTemplate,
            CategoryNode => CategoryTemplate,
            _ => base.SelectTemplateCore(item)
        };
    }
}