using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GIMI_ModManager.WinUI.Views.Controls;

/// <summary>
/// A border that can be toggled on and off with a bool. Use <c>BorderColor</c> and <c>BorderSize</c> to customize the border.
/// </summary>
[ContentProperty(Name = nameof(Children))]
public sealed partial class BoolBorder : UserControl
{
    public BoolBorder()
    {
        InitializeComponent();
        Children = RootBorder.Children;
    }


    public static readonly DependencyProperty ShowBorderProperty = DependencyProperty.Register(
        nameof(ShowBorder), typeof(bool), typeof(BoolBorder), new PropertyMetadata(default(bool)));

    public bool ShowBorder
    {
        get { return (bool)GetValue(ShowBorderProperty); }
        set
        {
            SetValue(ShowBorderProperty, value);
            SetBorderSize(BorderSize);
        }
    }


    public static readonly DependencyProperty BorderSizeProperty = DependencyProperty.Register(
        nameof(BorderSize), typeof(Thickness), typeof(BoolBorder), new PropertyMetadata(default(Thickness)));

    public Thickness BorderSize
    {
        get { return (Thickness)GetValue(BorderSizeProperty); }
        set
        {
            SetValue(BorderSizeProperty, value);
            SetBorderSize(value);
        }
    }


    public static readonly DependencyProperty BorderColorProperty = DependencyProperty.Register(
        nameof(BorderColor), typeof(Brush), typeof(BoolBorder), new PropertyMetadata(default(Brush)));

    public Brush BorderColor
    {
        get { return (Brush)GetValue(BorderColorProperty); }
        set { SetValue(BorderColorProperty, value); }
    }


    public static readonly DependencyProperty ChildrenProperty = DependencyProperty.Register(
        nameof(Children), typeof(UIElementCollection), typeof(BoolBorder),
        new PropertyMetadata(default(UIElementCollection)));

    public UIElementCollection Children
    {
        get { return (UIElementCollection)GetValue(ChildrenProperty); }
        set { SetValue(ChildrenProperty, value); }
    }


    public void SetBorderSize(Thickness thickness)
    {
        if (ShowBorder)
        {
            RootBorder.BorderThickness = thickness;
            RootBorder.Padding = new Thickness(0);
        }
        else
        {
            RootBorder.Padding = thickness;
            RootBorder.BorderThickness = new Thickness(0);
        }
    }
}