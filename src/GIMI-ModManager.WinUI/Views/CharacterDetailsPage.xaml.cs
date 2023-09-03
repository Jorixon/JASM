using Windows.ApplicationModel.DataTransfer;
using CommunityToolkit.WinUI.UI.Animations;
using CommunityToolkit.WinUI.UI.Controls;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using System.Diagnostics;
using Windows.Storage;
using Windows.System;
using Windows.UI;
using CommunityToolkit.WinUI.UI;
using GIMI_ModManager.Core.Entities;
using Serilog;
using GIMI_ModManager.WinUI.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace GIMI_ModManager.WinUI.Views;

public sealed partial class CharacterDetailsPage : Page
{
    public CharacterDetailsViewModel ViewModel { get; }

    private readonly MenuFlyout _modListContextMenuFlyout = new();

    public CharacterDetailsPage()
    {
        ViewModel = App.GetService<CharacterDetailsViewModel>();
        this.InitializeComponent();
        ViewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(ViewModel.IsAddingModFolder))
            {
                this.IsEnabled = !ViewModel.IsAddingModFolder;
            }

            if (args.PropertyName == nameof(ViewModel.ModListVM.Mods))
            {
                CheckIfAnyMods();
            }
        };

        ViewModel.ModListVM.SelectedMods.CollectionChanged += (sender, args) =>
        {
            foreach (var newSelectedMods in args?.NewItems?.OfType<NewModModel>() ?? new List<NewModModel>(0))
            {
                var equalItemInGrid = ModListGrid.ItemsSource.OfType<NewModModel>()
                    .FirstOrDefault(x => x.Id == newSelectedMods.Id);

                if (!ModListGrid.SelectedItems.OfType<NewModModel>().Contains(equalItemInGrid))
                    ModListGrid.SelectedItems.Add(equalItemInGrid);
            }

            foreach (var removedSelectedMods in args?.OldItems?.OfType<NewModModel>() ?? new List<NewModModel>(0))
            {
                var equalItemInGrid = ModListGrid.ItemsSource.OfType<NewModModel>()
                    .FirstOrDefault(x => x.Id == removedSelectedMods.Id);

                if (ModListGrid.SelectedItems.Contains(equalItemInGrid))
                    ModListGrid.SelectedItems.Remove(equalItemInGrid);
            }
        };

        ModListGrid.Loaded += (sender, args) =>
        {
            var modEntry = ModListGrid.ItemsSource.OfType<NewModModel>()?.FirstOrDefault(mod => mod.IsEnabled);
            ModListGrid.SelectedItem = modEntry;
            // set focus to the first item
            ModListGrid.Focus(FocusState.Programmatic);
        };
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        this.RegisterElementForConnectedAnimation("animationKeyContentGrid", itemHero);
        ViewModel.ModListVM.BackendMods.CollectionChanged += (sender, args) => CheckIfAnyMods();
        CheckIfAnyMods();
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        base.OnNavigatingFrom(e);
        if (e.NavigationMode == NavigationMode.Back)
        {
            var navigationService = App.GetService<INavigationService>();
            if (ViewModel.ShownCharacter != null!)
            {
                navigationService.SetListDataItemForNextConnectedAnimation(ViewModel.ShownCharacter);
            }
        }
    }

    private void CheckIfAnyMods()
    {
        if (ViewModel.ModListVM.BackendMods.Any())
        {
            var stackPanel = ModListArea.FindName("NoModsStackPanel") as StackPanel;
            if (stackPanel != null)
            {
                stackPanel.Visibility = Visibility.Collapsed;
            }

            ModListGrid.Visibility = Visibility.Visible;
        }
        else if (ModListArea.FindName("NoModsStackPanel") is null)
        {
            ModListGrid.Visibility = Visibility.Collapsed;
            var stackPanel = new StackPanel()
            {
                Name = "NoModsStackPanel",
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                AllowDrop = false,
            };

            var title = new TextBlock()
            {
                Text = "No mods found for this character 😖",
                FontSize = 28,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                AllowDrop = false,
            };
            stackPanel.Children.Add(title);


            var backgroundGrid = new Grid()
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                AllowDrop = false,
                Background = new SolidColorBrush(Colors.Transparent),
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
                AllowDrop = false,
            };

            // Create the TextBlock for "Drop Mods Here"
            var dropText = new TextBlock
            {
                Text = "Drop Mods Here",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 20,
                AllowDrop = false,
            };


            backgroundGrid.Children.Add(dottedLineBox);
            dottedLineBox.Child = dropText;

            ModListArea.Children.Add(stackPanel);
        }
    }

    private void ModListGrid_OnSorting(object? sender, DataGridColumnEventArgs e)
    {
        if (e.Column.Tag.ToString() == "Name")
        {
            //Implement sort on the column "Range" using LINQ
            if (e.Column.SortDirection == null || e.Column.SortDirection == DataGridSortDirection.Descending)
            {
                var sortedMods = from modEntry in ViewModel.ModListVM.BackendMods
                    orderby modEntry.Name ascending
                    select modEntry;

                ViewModel.ModListVM.ReplaceMods(sortedMods);


                e.Column.SortDirection = DataGridSortDirection.Ascending;
            }
            else
            {
                var sortedMods = from modEntry in ViewModel.ModListVM.BackendMods
                    orderby modEntry.Name descending
                    select modEntry;

                ViewModel.ModListVM.ReplaceMods(sortedMods);


                e.Column.SortDirection = DataGridSortDirection.Descending;
            }
        }

        if (e.Column.Tag.ToString() == "Folder Name")
        {
            //Implement sort on the column "Range" using LINQ
            if (e.Column.SortDirection == null || e.Column.SortDirection == DataGridSortDirection.Descending)
            {
                var sortedMods = from modEntry in ViewModel.ModListVM.BackendMods
                    orderby modEntry.FolderName ascending
                    select modEntry;

                ViewModel.ModListVM.ReplaceMods(sortedMods);


                e.Column.SortDirection = DataGridSortDirection.Ascending;
            }
            else
            {
                var sortedMods = from modEntry in ViewModel.ModListVM.BackendMods
                    orderby modEntry.FolderName descending
                    select modEntry;

                ViewModel.ModListVM.ReplaceMods(sortedMods);


                e.Column.SortDirection = DataGridSortDirection.Descending;
            }
        }


        if (e.Column.Tag.ToString() == "IsEnabled")
        {
            var enabledMods = ViewModel.ModListVM.BackendMods.Where(modEntry => modEntry.IsEnabled);
            var disabledMods = ViewModel.ModListVM.BackendMods.Where(modEntry => !modEntry.IsEnabled);

            if (e.Column.SortDirection == null || e.Column.SortDirection == DataGridSortDirection.Descending)
            {
                var sortedMods = enabledMods.Concat(disabledMods);

                ViewModel.ModListVM.ReplaceMods(sortedMods);
                e.Column.SortDirection = DataGridSortDirection.Ascending;
            }
            else
            {
                var sortedMods = disabledMods.Concat(enabledMods);

                ViewModel.ModListVM.ReplaceMods(sortedMods);
                e.Column.SortDirection = DataGridSortDirection.Descending;
            }
        }


        // Remove sorting indicators from other columns
        foreach (var dgColumn in ModListGrid.Columns)
        {
            if (dgColumn.Tag.ToString() != e.Column.Tag.ToString())
            {
                dgColumn.SortDirection = null;
            }
        }
    }

    private void ModListArea_OnDragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private async void ModListArea_OnDrop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            await ViewModel.DragAndDropCommand.ExecuteAsync(await e.DataView.GetStorageItemsAsync());
        }
    }

    private async void ModListGrid_OnCellEditEnded(object? sender, DataGridCellEditEndedEventArgs e)
    {
        var modModel = (NewModModel)e.Row.DataContext;
        await ViewModel.ChangeModDetails(modModel);
    }

    private void ModListGrid_OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Space)
        {
            ViewModel.ModList_KeyHandler(ModListGrid.SelectedItems.OfType<NewModModel>().Select(mod => mod.Id), e.Key);
            e.Handled = true;
        }
    }

    private void ModListGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.ModListVM.SelectionChanged(e.AddedItems.OfType<NewModModel>().ToArray(),
            e.RemovedItems.OfType<NewModModel>().ToArray());
    }


    private void ModRowFlyout_OnOpening(object? sender, object e)
    {
        if (!ViewModel.ModListVM.SelectedMods.Any())
            ModRowFlyout.Hide();

        ViewModel.MoveModsFlyoutVM.IsMoveModsFlyoutOpen = true;
        ViewModel.MoveModsFlyoutVM.SetSelectedModsCommand.Execute(ViewModel.ModListVM.SelectedMods);
    }

    private async void MoveModSearch_OnSuggestionChosen(AutoSuggestBox sender,
        AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        //sender.IsEnabled = false;
        ViewModel.MoveModsFlyoutVM.SearchText = ((GenshinCharacter)args.SelectedItem).DisplayName;
        userScrolling = true;
        //ViewModel.MoveModsFlyoutVM.SelectCharacterCommand.Execute(args.SelectedItem);
    }

    private bool userScrolling = false;

    private async void MoveModSearch_OnTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (sender.IsEnabled && args.Reason == AutoSuggestionBoxTextChangeReason.UserInput && userScrolling == false)
            await ViewModel.MoveModsFlyoutVM.TextChangedCommand.ExecuteAsync(sender.Text);

        userScrolling = false;
    }

    private void MoveRowFlyout_OnClosed(object? sender, object e)
    {
        ViewModel.MoveModsFlyoutVM.IsMoveModsFlyoutOpen = false;
        MoveModSearchBox.IsEnabled = true;
        userScrolling = false;
        ViewModel.MoveModsFlyoutVM.ResetStateCommand.Execute(null);
    }

    private void MoveModSearch_OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        sender.IsEnabled = false;
        ViewModel.MoveModsFlyoutVM.SelectCharacterCommand.Execute(args.ChosenSuggestion);
        MoveModsButton.Focus(FocusState.Programmatic);
    }
}