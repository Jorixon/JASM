using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using CommunityToolkit.WinUI.UI.Animations;
using CommunityToolkit.WinUI.UI.Controls;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Models;
using GIMI_ModManager.WinUI.Services.Notifications;
using GIMI_ModManager.WinUI.ViewModels;
using GIMI_ModManager.WinUI.ViewModels.SubVms;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Serilog;

namespace GIMI_ModManager.WinUI.Views;

public sealed partial class CharacterDetailsPage : Page
{
    public CharacterDetailsViewModel ViewModel { get; }

    public Visibility PageLoader = Visibility.Visible;

    private readonly MenuFlyout _modListContextMenuFlyout = new();

    private static (string, ModListVM.SortMethod)? _currentSortMethod;

    public CharacterDetailsPage()
    {
        ViewModel = App.GetService<CharacterDetailsViewModel>();
        InitializeComponent();
        ViewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(ViewModel.IsAddingModFolder)) IsEnabled = !ViewModel.IsAddingModFolder;

            if (args.PropertyName == nameof(ViewModel.ModListVM.Mods)) CheckIfAnyMods();

            if (args.PropertyName == nameof(ViewModel.SelectedInGameSkin))
                ShowOnlyOneSkinTooltip(!ViewModel.MultipleInGameSkins);
        };

        ModListGrid.Loaded += async (_, __) =>
        {
            try
            {
                SyncGridSelection(ViewModel.ModListVM.SelectedMods, new List<ModModel>());

                ViewModel.ModListVM.SelectedMods.CollectionChanged += (_, args) =>
                {
                    SyncGridSelection(args.NewItems?.OfType<ModModel>() ?? new List<ModModel>(),
                        args.OldItems?.OfType<ModModel>() ?? new List<ModModel>());
                };

                await Task.Delay(100);
                ModListGrid.Focus(FocusState.Programmatic);
            }
            finally
            {
                PageLoader = Visibility.Collapsed;
            }

            if (_currentSortMethod is null) return;
            var sortedColumn =
                ModListGrid.Columns.FirstOrDefault(x => x.Tag.ToString() == _currentSortMethod.Value.Item1);
            if (sortedColumn is null) return;

            sortedColumn.SortDirection = _currentSortMethod.Value.Item2.IsDescending
                ? DataGridSortDirection.Descending
                : DataGridSortDirection.Ascending;

            ModListGrid_OnSorting(this, new DataGridColumnEventArgs(sortedColumn));
        };


        ViewModel.MoveModsFlyoutVM.CloseFlyoutEvent += (sender, args) => { ModRowFlyout.Hide(); };
    }

    private void SyncGridSelection(IEnumerable<ModModel> newlySelectedMods,
        IEnumerable<ModModel> newlyRemovedSelectedMods)
    {
        foreach (var newSelectedMods in newlySelectedMods)
        {
            var equalItemInGrid = ModListGrid.ItemsSource.OfType<ModModel>()
                .FirstOrDefault(x => x.Id == newSelectedMods.Id);

            if (!ModListGrid.SelectedItems.OfType<ModModel>().Contains(equalItemInGrid))
                ModListGrid.SelectedItems.Add(equalItemInGrid);
        }

        foreach (var removedSelectedMods in newlyRemovedSelectedMods)
        {
            var equalItemInGrid = ModListGrid.ItemsSource.OfType<ModModel>()
                .FirstOrDefault(x => x.Id == removedSelectedMods.Id);

            if (ModListGrid.SelectedItems.Contains(equalItemInGrid))
                ModListGrid.SelectedItems.Remove(equalItemInGrid);
        }
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (itemHero != null) // Trying to fix an argument null exception
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
                navigationService.SetListDataItemForNextConnectedAnimation(ViewModel.ShownCharacter);
        }
    }

    private void CheckIfAnyMods()
    {
        if (ViewModel.ModListVM.BackendMods.Any())
        {
            var stackPanel = MainContentArea.FindName("NoModsStackPanel") as StackPanel;
            if (stackPanel != null) stackPanel.Visibility = Visibility.Collapsed;

            ModListGrid.Visibility = Visibility.Visible;
            ModDetailsPane.Visibility = Visibility.Visible;
            ModPaneSplitter.Visibility = Visibility.Visible;
            ModListArea.AllowDrop = true;
            MainContentArea.AllowDrop = false;
        }
        else if (MainContentArea.FindName("NoModsStackPanel") is null)
        {
            ModListGrid.Visibility = Visibility.Collapsed;
            ModDetailsPane.Visibility = Visibility.Collapsed;
            ModPaneSplitter.Visibility = Visibility.Collapsed;

            ModListArea.AllowDrop = false;
            MainContentArea.AllowDrop = true;

            var stackPanel = new StackPanel()
            {
                Name = "NoModsStackPanel",
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
        }
    }

    private void ModListGrid_OnSorting(object? sender, DataGridColumnEventArgs e)
    {
        if (e.Column.Tag.ToString() == "Name")
        {
            //Implement sort on the column "Range" using LINQ
            if (e.Column.SortDirection == null || e.Column.SortDirection == DataGridSortDirection.Descending)
            {
                var sortedMods =
                    from modEntry in ViewModel.ModListVM.BackendMods
                    orderby modEntry.Name ascending
                    select modEntry;

                ViewModel.ModListVM.ReplaceMods(sortedMods);


                e.Column.SortDirection = DataGridSortDirection.Ascending;
                ViewModel.SortMethod = new ModListVM.SortMethod("Name", false);
            }
            else
            {
                var sortedMods =
                    from modEntry in ViewModel.ModListVM.BackendMods
                    orderby modEntry.Name descending
                    select modEntry;

                ViewModel.ModListVM.ReplaceMods(sortedMods);


                e.Column.SortDirection = DataGridSortDirection.Descending;
                ViewModel.SortMethod = new ModListVM.SortMethod("Name", true);
            }

            PersistSortInMemory("Name");
        }

        if (e.Column.Tag.ToString() == "Folder Name")
        {
            //Implement sort on the column "Range" using LINQ
            if (e.Column.SortDirection == null || e.Column.SortDirection == DataGridSortDirection.Descending)
            {
                var sortedMods =
                    from modEntry in ViewModel.ModListVM.BackendMods
                    orderby modEntry.FolderName ascending
                    select modEntry;

                ViewModel.ModListVM.ReplaceMods(sortedMods);


                e.Column.SortDirection = DataGridSortDirection.Ascending;
                ViewModel.SortMethod = new ModListVM.SortMethod("FolderName", false);
            }
            else
            {
                var sortedMods =
                    from modEntry in ViewModel.ModListVM.BackendMods
                    orderby modEntry.FolderName descending
                    select modEntry;

                ViewModel.ModListVM.ReplaceMods(sortedMods);


                e.Column.SortDirection = DataGridSortDirection.Descending;
                ViewModel.SortMethod = new ModListVM.SortMethod("FolderName", true);
            }

            PersistSortInMemory("Folder Name");
        }


        if (e.Column.Tag.ToString() == "IsEnabled")
        {
            var enabledMods = ViewModel.ModListVM.BackendMods.Where(modEntry => modEntry.IsEnabled);
            var disabledMods = ViewModel.ModListVM.BackendMods.Where(modEntry => !modEntry.IsEnabled);

            if (e.Column.SortDirection == null || e.Column.SortDirection == DataGridSortDirection.Descending)
            {
                var sortedMods = enabledMods.Concat(disabledMods);

                e.Column.SortDirection = DataGridSortDirection.Ascending;
                ViewModel.SortMethod = new ModListVM.SortMethod("IsEnabled", true);
                ViewModel.ModListVM.ResetContent(ViewModel.SortMethod);
            }
            else
            {
                var sortedMods = disabledMods.Concat(enabledMods);

                e.Column.SortDirection = DataGridSortDirection.Descending;
                ViewModel.SortMethod = new ModListVM.SortMethod("IsEnabled", false);
                ViewModel.ModListVM.ResetContent(ViewModel.SortMethod);
            }

            PersistSortInMemory("IsEnabled");
        }

        if (e.Column.Tag.ToString() == "Author Name")
        {
            //Implement sort on the column "Range" using LINQ
            if (e.Column.SortDirection == null || e.Column.SortDirection == DataGridSortDirection.Descending)
            {
                var sortedMods = ViewModel.ModListVM.BackendMods
                    .OrderBy(m => string.IsNullOrWhiteSpace(m.Author))
                    .ThenByDescending(modModel => modModel.Author);


                ViewModel.ModListVM.ReplaceMods(sortedMods);


                e.Column.SortDirection = DataGridSortDirection.Ascending;
                ViewModel.SortMethod = new ModListVM.SortMethod(nameof(ModModel.Author), true);
            }
            else
            {
                var sortedMods = ViewModel.ModListVM.BackendMods
                    .OrderBy(m => string.IsNullOrWhiteSpace(m.Author))
                    .ThenBy(modModel => modModel.Author);

                ViewModel.ModListVM.ReplaceMods(sortedMods);


                e.Column.SortDirection = DataGridSortDirection.Descending;
                ViewModel.SortMethod = new ModListVM.SortMethod(nameof(ModModel.Author), false);
            }

            PersistSortInMemory("Author Name");
        }


        if (e.Column.Tag.ToString() == "Date Added")
        {
            //Implement sort on the column "Range" using LINQ
            if (e.Column.SortDirection == null || e.Column.SortDirection == DataGridSortDirection.Descending)
            {
                var sortedMods = ViewModel.ModListVM.BackendMods
                    .OrderByDescending(m => m.DateAdded);


                ViewModel.ModListVM.ReplaceMods(sortedMods);


                e.Column.SortDirection = DataGridSortDirection.Ascending;
                ViewModel.SortMethod = new ModListVM.SortMethod(nameof(ModModel.DateAdded), true);
            }
            else
            {
                var sortedMods = ViewModel.ModListVM.BackendMods
                    .OrderBy(m => m.DateAdded);

                ViewModel.ModListVM.ReplaceMods(sortedMods);


                e.Column.SortDirection = DataGridSortDirection.Descending;
                ViewModel.SortMethod = new ModListVM.SortMethod(nameof(ModModel.DateAdded), false);
            }

            PersistSortInMemory("Date Added");
        }


        // Remove sorting indicators from other columns
        foreach (var dgColumn in ModListGrid.Columns)
            if (dgColumn.Tag.ToString() != e.Column.Tag.ToString())
                dgColumn.SortDirection = null;

        void PersistSortInMemory(string tag)
        {
            if (ViewModel.SortMethod is not null)
                _currentSortMethod = new ValueTuple<string, ModListVM.SortMethod>(tag, ViewModel.SortMethod);
        }
    }

    private void ModListArea_OnDragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private async void ModListArea_OnDrop(object sender, DragEventArgs e)
    {
        var deferral = e.GetDeferral();
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
            await ViewModel.DragAndDropCommand.ExecuteAsync(await e.DataView.GetStorageItemsAsync());

        deferral.Complete();
    }

    private void ModListGrid_OnCellEditEnded(object? sender, DataGridCellEditEndedEventArgs e)
    {
        var modModel = (ModModel)e.Row.DataContext;
        ViewModel.ChangeModDetails(modModel);
    }

    private async void ModListGrid_OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Space)
        {
            await ViewModel.ModList_KeyHandler(ModListGrid.SelectedItems.OfType<ModModel>().Select(mod => mod.Id),
                e.Key);
            e.Handled = true;
        }

        if (e.Key == VirtualKey.Delete)
        {
            e.Handled = true;
            ViewModel.MoveModsFlyoutVM.SetSelectedModsCommand.Execute(ModListGrid.SelectedItems.OfType<ModModel>()
                .ToArray());
            await ViewModel.MoveModsFlyoutVM.DeleteModsCommand.ExecuteAsync(null);
            ViewModel.MoveModsFlyoutVM.ResetStateCommand.Execute(null);
        }
    }

    private void ModListGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.ModListVM.SelectionChanged(e.AddedItems.OfType<ModModel>().ToArray(),
            e.RemovedItems.OfType<ModModel>().ToArray());
    }


    private void ModRowFlyout_OnOpening(object? sender, object e)
    {
        if (!ViewModel.ModListVM.SelectedMods.Any())
            ModRowFlyout.Hide();

        ViewModel.MoveModsFlyoutVM.IsMoveModsFlyoutOpen = true;
        ViewModel.MoveModsFlyoutVM.SetSelectedModsCommand.Execute(ViewModel.ModListVM.SelectedMods);
    }

    private void MoveModSearch_OnSuggestionChosen(AutoSuggestBox sender,
        AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        //sender.IsEnabled = false;
        ViewModel.MoveModsFlyoutVM.SearchText = ((IModdableObject)args.SelectedItem).DisplayName;
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
        var anyCharacterFound = ViewModel.MoveModsFlyoutVM.SelectCharacter(args.ChosenSuggestion as IModdableObject);
        if (!anyCharacterFound)
            return;
        sender.IsEnabled = false;
        MoveModsButton.Focus(FocusState.Programmatic);
    }

    private void ModRowFlyout_OnOpened(object? sender, object e)
    {
        MoveModSearchBox.Focus(FocusState.Programmatic);
    }

    private void ModDetailsPaneImage_OnDragEnter(object sender, DragEventArgs e)
    {
        if (ViewModel.ModPaneVM.IsReadOnlyMode)
            return;
        e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private void ModDetailsPaneImage_OnDragOver(object sender, DragEventArgs e)
    {
        if (ViewModel.ModPaneVM.IsReadOnlyMode)
            return;
        e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private async void ModDetailsPaneImage_OnDrop(object sender, DragEventArgs e)
    {
        if (ViewModel.ModPaneVM.IsReadOnlyMode)
            return;

        var deferral = e.GetDeferral();
        if (e.DataView.Contains(StandardDataFormats.Uri))
        {
            var uri = await e.DataView.GetUriAsync();
            await ViewModel.ModPaneVM.SetImageFromDragDropWeb(uri);
        }
        else if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            await ViewModel.ModPaneVM.SetImageFromDragDropFile(await e.DataView.GetStorageItemsAsync());
        }

        deferral.Complete();
    }

    private void Image_OnImageFailed(object sender, ExceptionRoutedEventArgs e)
    {
        Log.Error("Failed to load mod preview image: {Error}", e.ErrorMessage);
        var image = (Image)sender;
    }

    private void ModNameCell_OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel.ModPaneVM.IsReadOnlyMode ||
            ViewModel.ModPaneVM.SelectedModModel.SettingsEquals(new ModModel()))
            return;

        ViewModel.ModPaneVM.IsEditingModName = true;
        ModNameTextBlock.SetFocus();
        ModNameTextBlock.TextSelectionStart = ModNameTextBlock.Text.Length;
    }

    private async void ImageFlyout_PasteImage(object sender, RoutedEventArgs e)
    {
        if (ViewModel.ModPaneVM.IsReadOnlyMode)
            return;

        var package = Clipboard.GetContent();
        if (package.Contains(StandardDataFormats.StorageItems))
        {
            await ViewModel.ModPaneVM.SetImageFromDragDropFile(await package.GetStorageItemsAsync());
        }
        else if (package.Contains(StandardDataFormats.Bitmap))
        {
            var imageStream = await package.GetBitmapAsync();
            if (imageStream is null) return;
            await ViewModel.ModPaneVM.SetImageFromBitmapStreamAsync(imageStream, package.AvailableFormats);
        }
    }

    private async void ImageFlyout_CopyImage(object sender, RoutedEventArgs e)
    {
        if (ViewModel.ModPaneVM.IsReadOnlyMode)
            return;

        var package = new DataPackage
        {
            RequestedOperation = DataPackageOperation.Copy
        };

        var imageFile =
            await StorageFile.GetFileFromPathAsync(ViewModel.ModPaneVM.SelectedModModel.ImagePath.LocalPath);
        var imageStream = RandomAccessStreamReference.CreateFromFile(imageFile);
        package.SetBitmap(imageStream);
        package.SetStorageItems(new List<IStorageItem>()
        {
            imageFile
        });

        Clipboard.SetContent(package);
        Clipboard.Flush();
    }

    private void NotificationButton_OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Button button) return;
        if (button.DataContext is not ModNotification modNotification) return;
        if (modNotification.AttentionType != AttentionType.UpdateAvailable) return;


        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
    }

    private void NotificationButton_OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
    }

    private async void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        if (button.DataContext is not ModNotification modNotification) return;
        if (modNotification.AttentionType != AttentionType.UpdateAvailable) return;

        await ViewModel.OpenNewModsWindowCommand.ExecuteAsync(modNotification).ConfigureAwait(false);
    }

    private void ShowOnlyOneSkinTooltip(bool showTooltip)
    {
        var button = SelectSkinBox;
        if (!showTooltip) return;

        var tooltip = ToolTipService.GetToolTip(button);
        if (tooltip is ToolTip) return;
        var toolTip = new ToolTip
        {
            Content = "This character only has one default in-game skin, so you can't change it.",
            Placement = PlacementMode.Bottom
        };

        ToolTipService.SetToolTip(button, toolTip);
    }

    private async void ViewToggleSwitch_OnToggled(object sender, RoutedEventArgs e)
    {
        if (ViewModel.GoToGalleryScreenCommand.CanExecute(null))
            await ViewModel.GoToGalleryScreenCommand.ExecuteAsync(null);
    }
}