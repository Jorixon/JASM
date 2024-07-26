using CommunityToolkitWrapper;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Models.Settings;
using Microsoft.Graphics.Display;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;

namespace GIMI_ModManager.WinUI.Services.AppManagement;

public class WindowManagerService : IWindowManagerService
{
    private readonly ILogger _logger;
    private readonly IThemeSelectorService _themeSelectorService;
    private readonly List<Tuple<WindowEx, object>> _windows = new();
    private readonly List<WindowEx> _windowDialogOpen = new();

    private object _windowLock = new();

    public IReadOnlyCollection<WindowEx> Windows => _windows.Select(x => x.Item1).ToArray().AsReadOnly();

    public WindowEx MainWindow => App.MainWindow;

    private void AddWindow(WindowEx window, object? identifier = null)
    {
        lock (_windowLock)
        {
            window.Closed += (_, _) => { RemoveWindow(window); };
            _windows.Add(new Tuple<WindowEx, object>(window, identifier ?? window));
        }
    }

    private void RemoveWindow(WindowEx window)
    {
        lock (_windowLock)
        {
            _windows.RemoveAll(x =>
            {
                if (!x.Item1.Equals(window)) return false;

                DisposeWindow(window);
                return true;
            });
        }
    }

    public WindowManagerService(ILogger logger, IThemeSelectorService themeSelectorService)
    {
        _themeSelectorService = themeSelectorService;
        _logger = logger.ForContext<WindowManagerService>();
    }

    public void ShowWindow(WindowEx window)
    {
        GetWindow(window).Show();
    }


    public void ResizeWindow(WindowEx window, int width, int height)
    {
        var windowToResize = window;
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
            Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(window));
        var info = DisplayInformation.CreateForWindowId(windowId);
        var display = WindowsDisplayAPI.Display.GetDisplays().FirstOrDefault(d => d.IsGDIPrimary)?.CurrentSetting;
        if (display is null)
        {
            _logger?.Error("Could not get display information.");
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
            _logger?.Error(e, "Could not close window.");
        }
    }


    public WindowEx CreateWindow(UIElement windowContent, bool activate = true)
    {
        var window = new WindowEx();
        AddWindow(window);
        window.Content = windowContent;
        if (activate)
            window.Activate();
        return window;
    }

    public void CreateWindow(WindowEx window, object? identifier, bool activate = true)
    {
        AddWindow(window, identifier);
        window.Show();
        if (activate)
        {
            window.BringToFront();
        }
    }

    public WindowEx? GetWindow(object identifier)
    {
        return _windows.Find(x => x.Item2.Equals(identifier))?.Item1;
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
            dialog.RequestedTheme = _themeSelectorService.Theme;
            result = await dialog.ShowAsync();
        }
        finally
        {
            _windowDialogOpen.Remove(currentWindow);
        }

        return result;
    }


    public async Task<ContentDialogResult> ShowFullScreenDialogAsync(FrameworkElement content, XamlRoot xamlRoot,
        WindowEx window)
    {
        if (_windowDialogOpen.Contains(window))
            throw new InvalidOperationException("Window already has a dialog open.");


        _windowDialogOpen.Add(window);

        var dialog = new ContentDialog
        {
            Content = content,
            XamlRoot = xamlRoot,
            RequestedTheme = _themeSelectorService.Theme,
            Resources =
            {
                ["ContentDialogMaxWidth"] = 8000,
                ["ContentDialogMaxHeight"] = 4000
            },
            CloseButtonText = "Close"
        };

        dialog.SizeChanged += (s, e) =>
        {
            const int margin = 300;
            content.MinWidth = window.Width - margin;
            content.MinHeight = window.Height - margin;
            content.MaxWidth = window.Width - margin;
            content.MaxHeight = window.Height - margin;
        };

        if (content is IDisposable element)
        {
            dialog.Closed += (_, _) => element.Dispose();
        }

        if (content is IClosableElement closable)
        {
            closable.CloseRequested += (_, _) => dialog.Hide();
        }

        return await window.DispatcherQueue.EnqueueAsync(async () =>
        {
            try
            {
                var result = await dialog.ShowAsync();
                dialog.Content = null;
                return result;
            }
            finally
            {
                _windowDialogOpen.Remove(window);
            }
        }).ConfigureAwait(false);
    }


    public Task CloseWindowsAsync()
    {
        var task = new TaskCompletionSource();
        lock (_windowLock)
        {
            var window = new List<WindowEx>(_windows.Select(tup => tup.Item1)).ToArray();

            foreach (var windowEx in window)
                try
                {
                    DisposeWindow(windowEx);
                    windowEx.Close();
                    _windows.RemoveAll(x => x.Item1.Equals(windowEx));
                }
                catch (Exception e)
                {
                    _logger?.Error(e, "Could not close window.");
                }
        }

        task.TrySetResult();
        return task.Task;
    }

    private WindowEx GetWindow(WindowEx window)
    {
        lock (_windowLock)
        {
            return _windows.Find(x => x.Item1.Equals(window))?.Item1 ??
                   throw new ArgumentException("Window not found.", nameof(window));
        }
    }

    private void DisposeWindow(WindowEx window)
    {
        lock (_windowLock)
        {
            if (window.Content is IDisposable disposable)
                disposable.Dispose();
            if (window is IDisposable disposableWindow)
                disposableWindow.Dispose();
        }
    }
}

public interface IWindowManagerService
{
    public WindowEx MainWindow { get; }
    public IReadOnlyCollection<WindowEx> Windows { get; }
    void ShowWindow(WindowEx window);
    void ResizeWindow(WindowEx window, int width, int height);
    void ResizeWindow(WindowEx window, ScreenSizeSettings newSize);
    void ResizeWindowPercent(WindowEx window, int widthPercent, int heightPercent);
    void CloseWindow(WindowEx window);
    WindowEx CreateWindow(UIElement windowContent, bool activate = true);
    void CreateWindow(WindowEx window, object? identifier, bool activate = true);
    WindowEx? GetWindow(object identifier);
    Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog, WindowEx? window = null);

    Task<ContentDialogResult> ShowFullScreenDialogAsync(FrameworkElement content, XamlRoot xamlRoot,
        WindowEx window);

    Task CloseWindowsAsync();
}

public interface IClosableElement
{
    public event EventHandler? CloseRequested;
}