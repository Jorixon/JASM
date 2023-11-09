using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Streams;
using Windows.System;
using GIMI_ModManager.WinUI.Contracts.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GIMI_ModManager.WinUI.Views;

public sealed partial class EasterEggPage : Page
{
    public MediaPlayer? MediaPlayer { get; private set; }

    private IRandomAccessStream Stream = typeof(App).Assembly.GetManifestResourceStream
        ("GIMI_ModManager.WinUI.Assets.easterEgg.mp3").AsRandomAccessStream();

    public EasterEggPage()
    {
        InitializeComponent();
        Unloaded += (sender, args) =>
        {
            MediaPlayer?.Pause();
            MediaPlayer = null;
            _mediaPlayer?.Pause();
            MediaPlayer = null;
            Stream.Dispose();
        };
    }


    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        MediaPlayer = new MediaPlayer();
        MediaPlayer.Source =
            MediaSource.CreateFromStream(Stream, "audio/mp3");
        MediaPlayer.Play();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        MediaPlayer?.Pause();
        MediaPlayer = null;
        _mediaPlayer?.Pause();
        MediaPlayer = null;
        App.GetService<INavigationService>().Frame?.BackStack.Clear();
    }


    private async void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(new Uri("https://gamebanana.com/tools/14574"));
        await Launcher.LaunchUriAsync(new Uri("https://www.youtube.com/watch?v=dQw4w9WgXcQ?autoplay=1"));
    }

    private bool _isFailed;

    private void Image_OnImageFailed(object sender, ExceptionRoutedEventArgs e)
    {
        if (_isFailed)
        {
            return;
        }

        _isFailed = true;
        Image.Source = new BitmapImage(new Uri("https://media.tenor.com/cxBKOemPWZIAAAAC/pepe-clap.gif"));
    }

    private MediaPlayer? _mediaPlayer;

    private void UIElement_OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        //https://www.myinstants.com/media/sounds/999-social-credit-siren.mp3
        _mediaPlayer = new MediaPlayer();

        _mediaPlayer.Source =
            MediaSource.CreateFromUri(new Uri("https://www.myinstants.com/media/sounds/999-social-credit-siren.mp3"));

        _mediaPlayer.Play();
        MediaPlayer?.Pause();
    }

    private void UIElement_OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _mediaPlayer?.Pause();
        _mediaPlayer = null;
        MediaPlayer?.Play();
    }
}