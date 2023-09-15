

using Windows.System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace GIMI_ModManager.WinUI.Views.Controls;

public sealed partial class EditableTextBlock : UserControl
{
    public EditableTextBlock()
    {
        this.InitializeComponent();
    }

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(EditableTextBlock), new PropertyMetadata(default(string)));

    public string Text
    {
        get { return (string)GetValue(TextProperty); }
        set { SetValue(TextProperty, value); }
    }

    public static readonly DependencyProperty IsEditModeProperty = DependencyProperty.Register(
        nameof(IsEditMode), typeof(bool), typeof(EditableTextBlock), new PropertyMetadata(default(bool)));

    public bool IsEditMode
    {
        get { return (bool)GetValue(IsEditModeProperty); }
        set
        {
            SetValue(IsEditModeProperty, value);
            UpdateVisibility();
        }
    }


    private void UpdateVisibility()
    {
        if (IsEditMode)
        {
            TextBlock.Visibility = Visibility.Collapsed;
            TextBox.Visibility = Visibility.Visible;
        }
        else
        {
            TextBlock.Visibility = Visibility.Visible;
            TextBox.Visibility = Visibility.Collapsed;
        }
    }


    public static readonly DependencyProperty TextAlignmentProperty = DependencyProperty.Register(
        nameof(TextAlignment), typeof(TextAlignment), typeof(EditableTextBlock), new PropertyMetadata(TextAlignment.Left));

    public TextAlignment TextAlignment
    {
        get { return (TextAlignment)GetValue(TextAlignmentProperty); }
        set { SetValue(TextAlignmentProperty, value); }
    }


    public static readonly DependencyProperty StyleProperty = DependencyProperty.Register(
        nameof(Style), typeof(Style), typeof(EditableTextBlock), new PropertyMetadata(default(Style)));

    public Style Style
    {
        get { return (Style)GetValue(StyleProperty); }
        set { SetValue(StyleProperty, value); }
    }


    private void TextBox_OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            IsEditMode = false;
        }
    }
}