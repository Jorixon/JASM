using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using GIMI_ModManager.WinUI.Services.AppManagement;
using GIMI_ModManager.WinUI.ViewModels;
using GIMI_ModManager.WinUI.Views.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace GIMI_ModManager.WinUI.Views;

public sealed partial class PresetPage : Page
{
    public PresetViewModel ViewModel { get; } = App.GetService<PresetViewModel>();

    public PresetPage()
    {
        InitializeComponent();
        PresetsList.DragItemsCompleted += PresetsList_DragItemsCompleted;
    }

    private async void PresetsList_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        if (args.DropResult == DataPackageOperation.Move && ViewModel.ReorderPresetsCommand.CanExecute(null))
        {
            await ViewModel.ReorderPresetsCommand.ExecuteAsync(null);
        }
    }

    private async void UIElement_OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var presetVm = (ModPresetVm)((EditableTextBlock)sender).DataContext;

        if (e.Key == VirtualKey.Enter && ViewModel.RenamePresetCommand.CanExecute(presetVm))
        {
            await ViewModel.RenamePresetCommand.ExecuteAsync(presetVm);
        }
    }

    private TextBlock CreateTextBlock(string text)
    {
        return new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.WrapWholeWords,
            IsTextSelectionEnabled = true
        };
    }

    private async void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "How presets work",
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close,
            Content = new StackPanel
            {
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text =
                            "A Preset is a list of Mods to enable and their preferences. JASM reads and stores mod preferences in the mods themselves in the file .JASM_ModConfig.json",
                        TextWrapping = TextWrapping.WrapWholeWords
                    },
                    new TextBlock
                    {
                        Text =
                            "When you create a new preset JASM creates a list of all enabled mods and the preferences stored in them. So when you apply the preset later it will enable only those mods and apply the preferences stored in the preset",
                        TextWrapping = TextWrapping.WrapWholeWords
                    },
                    new TextBlock
                    {
                        Text =
                            "You can allow JASM to handle 3Dmigoto reloading by starting the Elevator and checking the Auto Sync checkbox. But you can also do it ourself by checking the Show Manual Controls checkbox and saving/loading preferences manually and refreshing 3Dmigoto with the F10 key.",
                        TextWrapping = TextWrapping.WrapWholeWords
                    },

                    CreateTextBlock(
                        "In this JASM version there is no way to see what mods are in a preset... But if you reallly want to, you can find the preset configuration files in this path: %appdata%\\..\\local\\JASM\\ApplicationData_Genshin \n" +
                        "I suggest not changing anything as these files are not meant to be edited manually."),

                    CreateTextBlock(
                        "It is possible to simply ignore the preset part of this page and only use the manual controls to persist mod preferences."
                    )
                }
            }
        };

        await App.GetService<IWindowManagerService>().ShowDialogAsync(dialog).ConfigureAwait(false);
    }
}