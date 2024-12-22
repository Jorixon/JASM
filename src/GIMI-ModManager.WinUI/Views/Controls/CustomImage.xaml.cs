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

    public static readonly DependencyProperty ButtonHorizontalAlignmentProperty = DependencyProperty.Register(
        nameof(ButtonHorizontalAlignment), typeof(HorizontalAlignment), typeof(CustomImage),
        new PropertyMetadata(HorizontalAlignment.Right));

    public HorizontalAlignment ButtonHorizontalAlignment
    {
        get => (HorizontalAlignment)GetValue(ButtonHorizontalAlignmentProperty);
        set
        {
            SetValue(ButtonHorizontalAlignmentProperty, value);

            switch (value)
            {
                case HorizontalAlignment.Left:
                    EditButtonFontIcon.Glyph = "\uEB7E";
                    break;
                case HorizontalAlignment.Right:
                    EditButtonFontIcon.Glyph = "\uE70F";
                    break;
            }
        }
    }

    public static readonly DependencyProperty ButtonVerticalAlignmentProperty = DependencyProperty.Register(
        nameof(ButtonVerticalAlignment), typeof(VerticalAlignment), typeof(CustomImage),
        new PropertyMetadata(VerticalAlignment.Top));

    public VerticalAlignment ButtonVerticalAlignment
    {
        get => (VerticalAlignment)GetValue(ButtonVerticalAlignmentProperty);
        set => SetValue(ButtonVerticalAlignmentProperty, value);
    }


    // Paste context button

    public static readonly DependencyProperty PasteButtonCommandProperty = DependencyProperty.Register(
        nameof(PasteButtonCommand), typeof(ICommand), typeof(CustomImage), new PropertyMetadata(default(ICommand)));

    public ICommand PasteButtonCommand
    {
        get { return (ICommand)GetValue(PasteButtonCommandProperty); }
        set { SetValue(PasteButtonCommandProperty, value); }
    }

    // Copy context button

    public static readonly DependencyProperty CopyButtonCommandProperty = DependencyProperty.Register(
        nameof(CopyButtonCommand), typeof(ICommand), typeof(CustomImage), new PropertyMetadata(default(ICommand)));

    public ICommand CopyButtonCommand
    {
        get { return (ICommand)GetValue(CopyButtonCommandProperty); }
        set { SetValue(CopyButtonCommandProperty, value); }
    }
    // Clear context button

    public static readonly DependencyProperty ClearButtonCommandProperty = DependencyProperty.Register(
        nameof(ClearButtonCommand), typeof(ICommand), typeof(CustomImage), new PropertyMetadata(default(ICommand)));

    public ICommand ClearButtonCommand
    {
        get { return (ICommand)GetValue(ClearButtonCommandProperty); }
        set { SetValue(ClearButtonCommandProperty, value); }
    }


    public static readonly DependencyProperty EditButtonVisibilityProperty = DependencyProperty.Register(
        nameof(EditButtonVisibility), typeof(Visibility), typeof(CustomImage),
        new PropertyMetadata(default(Visibility)));

    public Visibility EditButtonVisibility
    {
        get { return (Visibility)GetValue(EditButtonVisibilityProperty); }
        set { SetValue(EditButtonVisibilityProperty, value); }
    }

    public static readonly DependencyProperty CopyButtonVisibilityProperty = DependencyProperty.Register(nameof(CopyButtonVisibility), typeof(Visibility),
        typeof(CustomImage), new PropertyMetadata(default(Visibility)));

    public Visibility CopyButtonVisibility
    {
        get => (Visibility)GetValue(CopyButtonVisibilityProperty);
        set => SetValue(CopyButtonVisibilityProperty, value);
    }
}