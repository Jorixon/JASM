using System.ComponentModel;
using GIMI_ModManager.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace GIMI_ModManager.WinUI.Views.CharacterManager;

public sealed partial class EditCharacterPage : Page
{
    public EditCharacterViewModel ViewModel { get; }

    public EditCharacterPage()
    {
        ViewModel = App.GetService<EditCharacterViewModel>();
        InitializeComponent();

        ViewModel.CharacterStatus.PropertyChanged += CharacterStatus_PropertyChanged;
        SetBackground();
    }

    private void CharacterStatus_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ViewModel.CharacterStatus.IsEnabled)
            or nameof(ViewModel.CharacterStatus.IsDisabled))
            SetBackground();
    }

    private void SetBackground()
    {
        var background = Application.Current.Resources["CardBackgroundFillColorDefaultBrush"] as SolidColorBrush;
        if (ViewModel.CharacterStatus.IsDisabled)
            background = Application.Current.Resources["SmokeFillColorDefaultBrush"] as SolidColorBrush;

        EditCharacterPageGrid.Background = background;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.OnNavigatedTo(e.Parameter);
    }
}