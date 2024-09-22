using CommunityToolkit.WinUI.UI.Animations;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Helpers.Xaml;
using GIMI_ModManager.WinUI.ViewModels.CharacterDetailsViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace GIMI_ModManager.WinUI.Views.CharacterDetailsPages;

public sealed partial class CharacterDetailsPage : Page
{
    public CharacterDetailsViewModel ViewModel { get; } = App.GetService<CharacterDetailsViewModel>();

    public CharacterDetailsPage()
    {
        InitializeComponent();
        CharacterCard.ViewModel = ViewModel;
        ModPane.ViewModel = ViewModel.ModPaneVM;
        ModGrid.ViewModel = ViewModel.ModGridVM;

        ViewModel.OnModObjectLoaded += OnModObjectLoaded;
        ViewModel.OnModsLoaded += OnModsLoaded;
        ViewModel.OnInitializingFinished += OnInitializingFinished;
    }

    private void OnModObjectLoaded(object? sender, EventArgs e)
    {
        ViewModel.GridLoadedAwaiter = () => ModGrid.DataGrid.AwaitItemsSourceLoaded(ViewModel.CancellationToken);
    }


    private void OnModsLoaded(object? sender, EventArgs e)
    {
        ViewModel.OnModsLoaded -= OnModsLoaded;
        PageInitLoader.Visibility = Visibility.Collapsed;
        RightWorkingArea.Visibility = Visibility.Visible;

        if (ModGrid.ViewModel.ModdableObjectHasAnyMods) return;
        ShowNoModsElement();
    }

    private void OnInitializingFinished(object? sender, EventArgs e)
    {
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        this.RegisterElementForConnectedAnimation("animationKeyContentGrid", CharacterCard.ItemHero);
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        base.OnNavigatingFrom(e);
        if (e.NavigationMode == NavigationMode.Back)
        {
            var navigationService = App.GetService<INavigationService>();
            if (ViewModel.ShownModObject != null!)
                navigationService.SetListDataItemForNextConnectedAnimation(ViewModel.ShownModObject);
        }
    }


    private void ShowNoModsElement()
    {
        var noModsElement = EnsureNoModsUIElementAdded();
        noModsElement.Visibility = Visibility.Visible;
        ModGrid.Visibility = Visibility.Collapsed;
        ModPane.Visibility = Visibility.Collapsed;
        ModPaneSplitter.Visibility = Visibility.Collapsed;

        ModListArea.AllowDrop = false;
        MainContentArea.AllowDrop = true;
    }

    private void HideNoModsElement()
    {
        var noModsElement = FindNoModsUIElement();
        if (noModsElement is not null)
            noModsElement.Visibility = Visibility.Collapsed;


        ModGrid.Visibility = Visibility.Visible;
        ModPane.Visibility = Visibility.Visible;
        ModPaneSplitter.Visibility = Visibility.Visible;

        ModListArea.AllowDrop = true;
        MainContentArea.AllowDrop = false;
    }

    private StackPanel? FindNoModsUIElement()
    {
        if (MainContentArea.FindName("NoModsStackPanel") is StackPanel existingStackPanel)
            return existingStackPanel;

        return null;
    }

    private StackPanel EnsureNoModsUIElementAdded()
    {
        var existingStackPanel = FindNoModsUIElement();
        if (existingStackPanel is not null)
            return existingStackPanel;

        var stackPanel = new StackPanel()
        {
            Name = "NoModsStackPanel",
            Visibility = Visibility.Collapsed,
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AllowDrop = false
        };

        var title = new TextBlock()
        {
            Text = "No mods found for this character 😖",
            FontSize = 28,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AllowDrop = false
        };
        stackPanel.Children.Add(title);


        var backgroundGrid = new Grid()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            AllowDrop = false,
            Background = new SolidColorBrush(Colors.Transparent)
        };

        stackPanel.Children.Add(backgroundGrid);

        var dottedLineBox = new Border
        {
            BorderBrush = Resources["SystemControlForegroundBaseMediumHighBrush"] as SolidColorBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),

            Width = 300,
            Height = 250,
            Margin = new Thickness(10),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(5),
            AllowDrop = false
        };

        // Create the TextBlock for "Drop Mods Here"
        var dropText = new TextBlock
        {
            Text = "Drop Mods Here",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 20,
            AllowDrop = false
        };


        backgroundGrid.Children.Add(dottedLineBox);
        dottedLineBox.Child = dropText;

        MainContentArea.Children.Add(stackPanel);

        return stackPanel;
    }


    private void KeyboardAccelerator_OnInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        SearchModsTextBox.Focus(FocusState.Keyboard);
    }

    private void SearchModsTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.SearchMods(SearchModsTextBox.Text);
    }
}