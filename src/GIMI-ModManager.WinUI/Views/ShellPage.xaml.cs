using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Helpers;
using GIMI_ModManager.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;
using Windows.UI.Core;
using Microsoft.UI.Xaml.Data;
using WindowActivatedEventArgs = Microsoft.UI.Xaml.WindowActivatedEventArgs;

namespace GIMI_ModManager.WinUI.Views;

public sealed partial class ShellPage : Page
{
    public ShellViewModel ViewModel { get; }

    public ShellPage(ShellViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        ViewModel.NavigationService.Frame = NavigationFrame;
        ViewModel.NavigationViewService.Initialize(NavigationViewControl);

        NavigationViewControl.IsPaneOpen = false;


        // TODO: Set the title bar icon by updating /Assets/WindowIcon.ico.
        // A custom title bar is required for full window theme and Mica support.
        // https://docs.microsoft.com/windows/apps/develop/title-bar?tabs=winui3#full-customization
        App.MainWindow.ExtendsContentIntoTitleBar = true;
        App.MainWindow.SetTitleBar(AppTitleBar);
        App.MainWindow.Activated += MainWindow_Activated;
        AppTitleBarText.Text = "AppDisplayName".GetLocalized();
#if DEBUG
        AppTitleBarText.Text += " - DEBUG";
#endif
        this.KeyDown += GlobalKeyHandler_Invoked;

        Loaded += (sender, args) =>
        {
            var bindings = new Binding()
            {
                Source = ViewModel,
                Path = new PropertyPath(nameof(ViewModel.SettingsInfoBadgeOpacity)),
                Mode = BindingMode.OneWay
            };

            var settingsItem = (NavigationViewItem)NavigationViewControl.SettingsItem;
            var infoBadge = new InfoBadge() { Opacity = 0, Value = 1 };
            settingsItem.InfoBadge = infoBadge;


            BindingOperations.SetBinding(settingsItem.InfoBadge, InfoBadge.OpacityProperty, bindings);

            Bindings.Update();
        };


#if RELEASE
// Hide debug menu in release mode
DebugItem.Visibility = Visibility.Collapsed;

#endif
    }


    private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        TitleBarHelper.UpdateTitleBar(RequestedTheme);

        KeyboardAccelerators.Add(BuildKeyboardAccelerator(VirtualKey.Left, VirtualKeyModifiers.Menu));
        KeyboardAccelerators.Add(BuildKeyboardAccelerator(VirtualKey.GoBack));
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        var resource = args.WindowActivationState == WindowActivationState.Deactivated
            ? "WindowCaptionForegroundDisabled"
            : "WindowCaptionForeground";

        AppTitleBarText.Foreground = (SolidColorBrush)App.Current.Resources[resource];
        App.AppTitlebar = AppTitleBarText as UIElement;
    }

    private void NavigationViewControl_DisplayModeChanged(NavigationView sender,
        NavigationViewDisplayModeChangedEventArgs args)
    {
        AppTitleBar.Margin = new Thickness()
        {
            Left = ViewModel.IsNotFirstTimeStartupPage
                ? sender.CompactPaneLength * (sender.DisplayMode == NavigationViewDisplayMode.Minimal ? 2 : 1)
                : 32,
            Top = AppTitleBar.Margin.Top,
            Right = AppTitleBar.Margin.Right,
            Bottom = AppTitleBar.Margin.Bottom
        };
    }

    private static KeyboardAccelerator BuildKeyboardAccelerator(VirtualKey key, VirtualKeyModifiers? modifiers = null)
    {
        var keyboardAccelerator = new KeyboardAccelerator() { Key = key };

        if (modifiers.HasValue)
        {
            keyboardAccelerator.Modifiers = modifiers.Value;
        }

        keyboardAccelerator.Invoked += OnKeyboardAcceleratorInvoked;

        return keyboardAccelerator;
    }

    private static void OnKeyboardAcceleratorInvoked(KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        var navigationService = App.GetService<INavigationService>();

        var result = navigationService.GoBack();

        args.Handled = result;
    }


    private async void GlobalKeyHandler_Invoked(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.F10)
            await ViewModel.RefreshGenshinMods();
    }
}