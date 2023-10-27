using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GIMI_ModManager.WinUI.Views.Controls;

public sealed partial class CustomImage : UserControl
{
    public CustomImage()
    {
        InitializeComponent();
    }


    public static readonly DependencyProperty ImageUriProperty = DependencyProperty.Register(
        nameof(ImageUri), typeof(Uri), typeof(CustomImage), new PropertyMetadata(default(Uri)));

    public Uri ImageUri
    {
        get => (Uri)GetValue(ImageUriProperty);
        set => SetValue(ImageUriProperty, value);
    }


    public static readonly DependencyProperty IsContextMenuEnabledProperty = DependencyProperty.Register(
        nameof(IsContextMenuEnabled), typeof(bool), typeof(CustomImage), new PropertyMetadata(default(bool)));

    public bool IsContextMenuEnabled
    {
        get => (bool)GetValue(IsContextMenuEnabledProperty);
        set
        {
            SetValue(IsContextMenuEnabledProperty, value);
            CustomImageControl.ContextFlyout = value ? CustomImageFlyout : null;
        }
    }

    public static readonly DependencyProperty EditButtonCommandProperty = DependencyProperty.Register(
        nameof(EditButtonCommand), typeof(ICommand), typeof(CustomImage), new PropertyMetadata(default(ICommand)));

    public ICommand EditButtonCommand
    {
        get { return (ICommand)GetValue(EditButtonCommandProperty); }
        set { SetValue(EditButtonCommandProperty, value); }
    }
}