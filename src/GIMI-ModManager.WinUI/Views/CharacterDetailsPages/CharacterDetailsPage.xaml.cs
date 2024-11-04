using Windows.ApplicationModel.DataTransfer;
using CommunityToolkit.WinUI.UI.Animations;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Helpers.Xaml;
using GIMI_ModManager.WinUI.ViewModels.CharacterDetailsViewModels;
using GIMI_ModManager.WinUI.ViewModels.CharacterDetailsViewModels.SubViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
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
        ModGrid.ViewModel.OnModsReloaded += OnModsReloaded;

        ViewModel.OnModObjectLoaded += OnModObjectLoaded;
        ViewModel.OnModsLoaded += OnModsLoaded;
        ViewModel.OnInitializingFinished += OnInitializingFinished;

        ViewModel.ContextMenuVM.CloseFlyout += ContextMenuVM_CloseFlyout;
    }


    private void OnModObjectLoaded(object? sender, EventArgs e)
    {
        ViewModel.OnModObjectLoaded -= OnModObjectLoaded;
        ViewModel.GridLoadedAwaiter = () => ModGrid.DataGrid.AwaitItemsSourceLoaded(ViewModel.CancellationToken);
        var button = CharacterCard.SelectSkinBox;

        if (!ViewModel.IsCharacter || ViewModel.Character.Skins.Count == 0) return;

        var tooltip = ToolTipService.GetToolTip(button);
        if (tooltip is ToolTip) return;
        var toolTip = new ToolTip
        {
            Content = "This character only has one default in-game skin, so you can't change it.",
            Placement = PlacementMode.Bottom
        };

        ToolTipService.SetToolTip(button, toolTip);
    }


    private void OnModsLoaded(object? sender, EventArgs e)
    {
        ViewModel.OnModsLoaded -= OnModsLoaded;
        PageInitLoader.Visibility = Visibility.Collapsed;
        RightWorkingArea.Visibility = Visibility.Visible;

        if (ModGrid.ViewModel.ModdableObjectHasAnyMods) return;
        ShowNoModsElement();
    }

    private void OnModsReloaded(object? sender, EventArgs eventArgs)
    {
        if (ViewModel.ModGridVM.ModdableObjectHasAnyMods)
            HideNoModsElement();
        else
            ShowNoModsElement();
    }

    private void OnInitializingFinished(object? sender, EventArgs e)
    {
        ViewModel.OnInitializingFinished -= OnInitializingFinished;
        ModGrid.DataGrid.ContextFlyout = ModRowFlyout;
        ModGrid.DataGrid.Focus(FocusState.Programmatic);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (CharacterCard?.ItemHero != null) // Trying to fix an argument null exception
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
        SearchModsTextBox.Visibility = Visibility.Collapsed;

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
        SearchModsTextBox.Visibility = Visibility.Visible;

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

    private async void ModListArea_OnDragEnter(object sender, DragEventArgs e)
    {
        var deferral = e.GetDeferral();
        if (e.DataView.Contains(StandardDataFormats.WebLink))
        {
            var uri = await e.DataView.GetWebLinkAsync();
            if (ViewModel.CanDragDropModUrl(uri))
                e.AcceptedOperation = DataPackageOperation.Copy;
        }
        else if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var storageItems = await e.DataView.GetStorageItemsAsync();
            if (ViewModel.CanDragDropMod(storageItems))
                e.AcceptedOperation = DataPackageOperation.Copy;
        }

        deferral.Complete();
    }

    private async void ModListArea_OnDrop(object sender, DragEventArgs e)
    {
        var deferral = e.GetDeferral();
        if (e.DataView.Contains(StandardDataFormats.WebLink))
        {
            await ViewModel.DragDropModUrlAsync(await e.DataView.GetWebLinkAsync());
        }
        else if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            await ViewModel.DragDropModAsync(await e.DataView.GetStorageItemsAsync());
        }

        deferral.Complete();
    }

    private void ViewToggleSwitch_OnToggled(object sender, RoutedEventArgs e)
    {
        if (ViewModel.GoToGalleryScreenCommand.CanExecute(null))
            ViewModel.GoToGalleryScreenCommand.ExecuteAsync(null);
    }

    #region ModRowFlyout

    private void ContextMenuVM_CloseFlyout(object? sender, EventArgs e) => ModRowFlyout.Hide();

    private void ModRowFlyout_OnOpening(object? sender, object e)
    {
        if (!ViewModel.ContextMenuVM.CanOpenContextMenu)
        {
            ModRowFlyout.Hide();
            return;
        }
    }

    private void ModRowFlyout_OnOpened(object? sender, object e) => MoveModSearchBox.Focus(FocusState.Programmatic);


    private void ModRowFlyout_OnClosing(FlyoutBase sender, FlyoutBaseClosingEventArgs args) => ViewModel.ContextMenuVM.OnFlyoutClosing();

    private void MoveModSearch_OnTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            ViewModel.ContextMenuVM.SearchTextChanged(sender.Text);
    }

    private void MoveModSearch_OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        ViewModel.ContextMenuVM.OnSuggestionChosen((SuggestedModObject)args.ChosenSuggestion);
        MoveModsButton.Focus(FocusState.Programmatic);
    }

    #endregion
}