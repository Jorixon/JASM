using GIMI_ModManager.WinUI.ViewModels.CharacterManagerViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

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


    public static readonly DependencyProperty InputFieldProperty = DependencyProperty.Register(
        nameof(InputField), typeof(BaseInputField), typeof(InputWrapper), new PropertyMetadata(new NoOpField()));

    public BaseInputField InputField
    {
        get { return (BaseInputField)GetValue(InputFieldProperty); }
        set
        {
            if (value != null!)
            {
                var binding = new Binding
                {
                    Source = value,
                    Path = new PropertyPath(nameof(BaseInputField.ValidationResults)),
                    Mode = BindingMode.OneWay
                };

                ValidationResultsListView.SetBinding(ItemsControl.ItemsSourceProperty, binding);
            }

            SetValue(InputFieldProperty, value);
        }
    }
}