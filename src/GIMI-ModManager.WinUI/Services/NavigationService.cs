using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.WinUI.UI.Animations;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Contracts.ViewModels;
using GIMI_ModManager.WinUI.Helpers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace GIMI_ModManager.WinUI.Services;

// For more information on navigation between pages see
// https://github.com/microsoft/TemplateStudio/blob/main/docs/WinUI/navigation.md
public class NavigationService : INavigationService
{
    private readonly IPageService _pageService;
    private object? _lastParameterUsed;
    private Frame? _frame;
    private readonly List<NavigationHistoryItem> _navigationHistory = new();

    public event NavigatedEventHandler? Navigated;

    public Frame? Frame
    {
        get
        {
            if (_frame == null)
            {
                _frame = App.MainWindow.Content as Frame;
                RegisterFrameEvents();
            }

            return _frame;
        }

        set
        {
            UnregisterFrameEvents();
            _frame = value;
            RegisterFrameEvents();
        }
    }

    [MemberNotNullWhen(true, nameof(Frame), nameof(_frame))]
    public bool CanGoBack => Frame != null && Frame.CanGoBack;

    [MemberNotNullWhen(true, nameof(Frame), nameof(_frame))]
    public bool CanGoForward => Frame != null && Frame.CanGoForward;

    public NavigationService(IPageService pageService)
    {
        _pageService = pageService;
    }

    private void RegisterFrameEvents()
    {
        if (_frame != null)
        {
            _frame.Navigated += OnNavigated;
        }
    }

    private void UnregisterFrameEvents()
    {
        if (_frame != null)
        {
            _frame.Navigated -= OnNavigated;
        }
    }

    public bool GoForward()
    {
        if (CanGoForward)
        {
            var vmBeforeNavigation = _frame.GetPageViewModel();
            var pageStackEntry = _frame.ForwardStack.Last();
            _frame.GoForward();
            if (vmBeforeNavigation is INavigationAware navigationAware)
            {
                navigationAware.OnNavigatedFrom();
            }

            _navigationHistory.Add(new NavigationHistoryItem(pageStackEntry));
            return true;
        }

        return false;
    }

    public bool GoBack()
    {
        if (CanGoBack)
        {
            var vmBeforeNavigation = _frame.GetPageViewModel();
            var pageStackEntry = _frame.BackStack.Last();
            _frame.GoBack();
            if (vmBeforeNavigation is INavigationAware navigationAware)
            {
                navigationAware.OnNavigatedFrom();
            }

            _navigationHistory.Add(new NavigationHistoryItem(pageStackEntry));
            return true;
        }

        return false;
    }

    public bool NavigateTo(string pageKey, object? parameter = null, bool clearNavigation = false)
    {
        var pageType = _pageService.GetPageType(pageKey);

        if (_frame != null && (_frame.Content?.GetType() != pageType ||
                               (parameter != null && !parameter.Equals(_lastParameterUsed))))
        {
            _frame.Tag = clearNavigation;
            var vmBeforeNavigation = _frame.GetPageViewModel();
            var navigated = _frame.Navigate(pageType, parameter);
            if (navigated)
            {
                _lastParameterUsed = parameter;
                if (vmBeforeNavigation is INavigationAware navigationAware)
                {
                    navigationAware.OnNavigatedFrom();
                }
            }

            _navigationHistory.Add(new NavigationHistoryItem(pageType, parameter));

            return navigated;
        }

        return false;
    }

    private void OnNavigated(object sender, NavigationEventArgs e)
    {
        if (sender is Frame frame)
        {
            var clearNavigation = (bool)frame.Tag;

            if (clearNavigation)
            {
                frame.BackStack.Clear();
            }

            const int maxBackStackEntries = 6;
            if (frame.BackStackDepth > maxBackStackEntries)
            {
                for (int i = 0; i < maxBackStackEntries - 1; i++)
                {
                    frame.BackStack.RemoveAt(0);
                    GC.Collect();
                }
            }

            if (_navigationHistory.Count > maxBackStackEntries)
            {
                _navigationHistory.RemoveRange(0, maxBackStackEntries - 1);
            }

            if (frame.GetPageViewModel() is INavigationAware navigationAware)
            {
                navigationAware.OnNavigatedTo(e.Parameter);
            }

            Navigated?.Invoke(sender, e);
        }
    }

    public void SetListDataItemForNextConnectedAnimation(object item) =>
        Frame.SetListDataItemForNextConnectedAnimation(item);

    // Get BackStackItems from Frame

    public ICollection<PageStackEntry> GetBackStackItems() => Frame?.BackStack ?? Array.Empty<PageStackEntry>();
    public IReadOnlyCollection<NavigationHistoryItem> GetNavigationHistory() => _navigationHistory.ToArray();
}