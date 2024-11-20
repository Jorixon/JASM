using CommunityToolkit.Mvvm.Input;
using GIMI_ModManager.Core.Helpers;
using GIMI_ModManager.WinUI.ViewModels.SubVms;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GIMI_ModManager.WinUI.Views.Controls;

public sealed partial class FolderSelector : UserControl
{
    public FolderSelector()
    {
        InitializeComponent();
        BrowseCommand = new AsyncRelayCommand(() => Task.CompletedTask);
    }

    public event EventHandler<StringEventArgs>? PathChangedEvent;


    #region ValidationErrorText

    private static readonly DependencyProperty ValidationErrorTextProperty = DependencyProperty.Register(
        nameof(ValidationErrorText), typeof(ICollection<InfoMessage>), typeof(FolderSelector),
        new PropertyMetadata(default(ICollection<InfoMessage>)));

    public ICollection<InfoMessage> ValidationErrorText
    {
        get => (ICollection<InfoMessage>)GetValue(ValidationErrorTextProperty);
        set => SetValue(ValidationErrorTextProperty, value);
    }

    #endregion

    #region Title

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(FolderSelector), new PropertyMetadata("Folder:"));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    #endregion

    #region Footer

    private static readonly DependencyProperty FooterProperty = DependencyProperty.Register(
        nameof(Footer), typeof(string), typeof(FolderSelector), new PropertyMetadata(default(string)));

    public string Footer
    {
        get { return (string)GetValue(FooterProperty); }
        set
        {
            FooterWrapper.Visibility = value.IsNullOrEmpty() ? Visibility.Collapsed : Visibility.Visible;
            SetValue(FooterProperty, value);
        }
    }

    #endregion

    #region SelectedFolderValue

    public static readonly DependencyProperty SelectedFolderValueProperty = DependencyProperty.Register(
        nameof(SelectedFolderValue), typeof(string), typeof(FolderSelector), new PropertyMetadata(default(string)));

    public string SelectedFolderValue
    {
        get => (string)GetValue(SelectedFolderValueProperty);
        set => SetValue(SelectedFolderValueProperty, value);
    }

    #endregion

    #region BrowseCommand

    private static readonly DependencyProperty BrowseCommandProperty = DependencyProperty.Register(
        nameof(BrowseCommand), typeof(IAsyncRelayCommand), typeof(FolderSelector),
        new PropertyMetadata(default));

    public IAsyncRelayCommand BrowseCommand
    {
        get => (IAsyncRelayCommand)GetValue(BrowseCommandProperty);
        set => SetValue(BrowseCommandProperty, value);
    }

    #endregion

    #region PlaceHolderText

    private static readonly DependencyProperty PlaceHolderTextProperty = DependencyProperty.Register(
        nameof(PlaceHolderText), typeof(string), typeof(FolderSelector), new PropertyMetadata(default(string)));

    public string PlaceHolderText
    {
        get { return (string)GetValue(PlaceHolderTextProperty); }
        set { SetValue(PlaceHolderTextProperty, value); }
    }

    #endregion


    private void SelectedFolderTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = (sender as TextBox)?.Text;
        PathChangedEvent?.Invoke(this, new StringEventArgs(text));
    }

    public class StringEventArgs : EventArgs
    {
        public StringEventArgs(string? value)
        {
            Value = value;
        }

        public string? Value { get; set; }
    }
}