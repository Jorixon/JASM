using GIMI_ModManager.WinUI.Services.AppManagement;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GIMI_ModManager.WinUI.ViewModels.SettingsViewModels;

internal class CharacterSkinsDialog
{
    private readonly IWindowManagerService _windowManagerService = App.GetService<IWindowManagerService>();

    public async Task<ContentDialogResult> ShowDialogAsync(bool isEnabled)
    {
        var dialog = new ContentDialog()
        {
            Title = isEnabled ? DisableTitle : EnableTitle,
            Content = new TextBlock()
            {
                Text = isEnabled ? DisableContent : EnableContent,
                TextWrapping = TextWrapping.WrapWholeWords,
                IsTextSelectionEnabled = true
            },
            DefaultButton = ContentDialogButton.Primary,
            PrimaryButtonText = isEnabled ? DisablePrimaryButtonText : EnablePrimaryButtonText,
            CloseButtonText = "Cancel"
        };


        return await _windowManagerService.ShowDialogAsync(dialog).ConfigureAwait(false);
    }

    private const string EnableTitle = "Enable Character Skins as Characters?";

    private const string EnableContent =
        "Enabling this will make JASM treat in game skins as separate characters in the character overview.\n" +
        "This could potentially become the default setting of JASM in the future.\n" +
        "JASM will not move any of your mods nor will it delete any.\n\n" +
        "Are you sure you want to enable character skins as characters? JASM will restart afterwards...";

    private const string EnablePrimaryButtonText = "Enable";

    private const string DisableTitle = "Disable Character Skins as Characters?";

    private const string DisableContent =
        "Disabling this will make JASM treat in game skins as skins of the base character in the character overview.\n" +
        "This is currently the default setting of JASM\n" +
        "JASM will not move any of your mods nor will it delete any.\n\n" +
        "Are you sure you want to disable character skins as characters? JASM will restart afterwards...";

    private const string DisablePrimaryButtonText = "Disable";
}