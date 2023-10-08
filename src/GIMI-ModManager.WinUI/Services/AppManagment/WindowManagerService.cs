using GIMI_ModManager.WinUI.Models.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Graphics.Display;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GIMI_ModManager.WinUI.Services.AppManagment;

public class WindowManagerService : IWindowManagerService
{
    private readonly ILogger? _logger;
    private readonly List<WindowEx> _windows = new();
    private readonly List<WindowEx> _windowDialogOpen = new();

    public WindowEx MainWindow => App.MainWindow;

    public WindowManagerService(ILogger? logger = null)
    {
        _logger = logger;
        _windows.Add(App.MainWindow);
        App.MainWindow.Closed += (sender, args) =>
        {
            _windows.Remove(App.MainWindow);
            var window = new List<WindowEx>(_windows);
            foreach (var windowEx in window)
                try
                {
                    windowEx.Close();
                }
                catch (Exception e)
                {
                    _logger?.LogError(e, "Could not close window.");
                }

            Application.Current.Exit();
        };
    }

    public void ShowWindow(WindowEx window)
    {
        GetWindow(window).Show();
    }


    public void ResizeWindow(WindowEx window, int width, int height)
    {
        var windowToResize = GetWindow(window);
        windowToResize.Width = width;
        windowToResize.Height = height;
    }

    public void ResizeWindow(WindowEx window, ScreenSizeSettings newSize)
    {
        ResizeWindow(window, newSize.Width, newSize.Height);
    }


    public void ResizeWindowPercent(WindowEx window, int widthPercent, int heightPercent)
    {
        var windowId =
            Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(GetWindow(window)));
        var info = DisplayInformation.CreateForWindowId(windowId);
        var display = WindowsDisplayAPI.Display.GetDisplays().FirstOrDefault(d => d.IsGDIPrimary)?.CurrentSetting;
        if (display is null)
        {
            _logger?.LogError("Could not get display information.");
            return;
        }

        var rawHeight = display.Resolution.Height;
        var rawWidth = display.Resolution.Width;
        var height = rawHeight * heightPercent / 100;
        var width = rawWidth * widthPercent / 100;
        ResizeWindow(window, width, height);
    }

    public void CloseWindow(WindowEx window)
    {
        try
        {
            GetWindow(window).Close();
        }
        catch (Exception e)
        {
            _logger?.LogError(e, "Could not close window.");
        }
    }


    public WindowEx CreateWindow(UIElement windowContent, bool activate = true)
    {
        var window = new WindowEx();
        _windows.Add(window);
        window.Closed += (sender, args) => _windows.Remove(window);
        window.Content = windowContent;
        if (activate)
            window.Activate();
        return window;
    }


    public async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog, WindowEx? window = null)
    {
        var currentWindow = window is not null ? GetWindow(window) : MainWindow;

        if (_windowDialogOpen.Contains(currentWindow))
            throw new InvalidOperationException("Window already has a dialog open.");


        _windowDialogOpen.Add(currentWindow);
        var result = ContentDialogResult.None;
        try
        {
            dialog.XamlRoot = currentWindow.Content.XamlRoot;
            result = await dialog.ShowAsync();
        }
        finally
        {
            _windowDialogOpen.Remove(currentWindow);
        }

        return result;
    }

    private WindowEx GetWindow(WindowEx window)
    {
        return _windows.Find(x => x.Equals(window)) ?? throw new ArgumentException("Window not found.", nameof(window));
    }
}

public interface IWindowManagerService
{
    public WindowEx MainWindow { get; }
    void ShowWindow(WindowEx window);
    void ResizeWindow(WindowEx window, int width, int height);
    void ResizeWindow(WindowEx window, ScreenSizeSettings newSize);
    void ResizeWindowPercent(WindowEx window, int widthPercent, int heightPercent);
    void CloseWindow(WindowEx window);
    WindowEx CreateWindow(UIElement windowContent, bool activate = true);
    Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog, WindowEx? window = null);
}