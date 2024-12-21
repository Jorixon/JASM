using GIMI_ModManager.WinUI.ViewModels.CharacterManagerViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GIMI_ModManager.WinUI.Views.CharacterManager;

public sealed partial class InputWrapper : UserControl
{
    public InputWrapper()
    {
        InitializeComponent();
    }


    public static readonly DependencyProperty HelpInfoProperty = DependencyProperty.Register(nameof(HelpInfo), typeof(FrameworkElement),
        typeof(InputWrapper), new PropertyMetadata(null));

    public static readonly DependencyProperty InputProperty = DependencyProperty.Register(nameof(Input), typeof(FrameworkElement),
        typeof(InputWrapper), new PropertyMetadata(null));

    public FrameworkElement Input
    {
        get => (FrameworkElement)GetValue(InputProperty);
        set => SetValue(InputProperty, value);
    }

    public FrameworkElement? HelpInfo
    {
        get => (FrameworkElement?)GetValue(HelpInfoProperty);
        set
        {
            if (value != null)
            {
                HelpButton.Visibility = Visibility.Visible;
            }

            SetValue(HelpInfoProperty, value);
        }
    }


    public static readonly DependencyProperty InputFieldViewModelProperty = DependencyProperty.Register(
        nameof(InputFieldViewModel), typeof(BaseInputFieldViewModel), typeof(InputWrapper), new PropertyMetadata(default(BaseInputFieldViewModel)));

    public BaseInputFieldViewModel InputFieldViewModel
    {
        get { return (BaseInputFieldViewModel)GetValue(InputFieldViewModelProperty); }
        set { SetValue(InputFieldViewModelProperty, value); }
    }
}