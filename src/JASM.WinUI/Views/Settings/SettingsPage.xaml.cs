﻿using GIMI_ModManager.WinUI.ViewModels;
using GIMI_ModManager.WinUI.Views.Controls;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace GIMI_ModManager.WinUI.Views;

// TODO: Set the URL for your privacy policy by updating SettingsPage_PrivacyTermsLink.NavigateUri in Resources.resw.
public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        ViewModel = App.GetService<SettingsViewModel>();
        InitializeComponent();
    }

    private void GimiFolder_OnPathChangedEvent(object? sender, FolderSelector.StringEventArgs e)
        => ViewModel.PathToGIMIFolderPicker.Validate(e.Value);


    private void ModsFolder_OnPathChangedEvent(object? sender, FolderSelector.StringEventArgs e)
        => ViewModel.PathToModsFolderPicker.Validate(e.Value);

    private async void LanguageSelectorComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0) return;
        var item = (string)e.AddedItems[0];
        await ViewModel.SelectLanguageCommand.ExecuteAsync(item).ConfigureAwait(false);
    }

    private async void GameSelectorComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0) return;
        var item = (string)e.AddedItems[0];
        await ViewModel.SelectGameCommand.ExecuteAsync(item).ConfigureAwait(false);
    }

    private void LocalCacheSlider_OnValueChanged(object _, RangeBaseValueChangedEventArgs e)
    {
        if (ViewModel.SetCacheLimitCommand.CanExecute((int)e.NewValue))
            ViewModel.SetCacheLimitCommand.ExecuteAsync((int)e.NewValue);
    }
}