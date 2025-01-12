using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.WinUI.UI.Animations;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Contracts.ViewModels;
using GIMI_ModManager.WinUI.Helpers;
using GIMI_ModManager.WinUI.Models.Settings;
using GIMI_ModManager.WinUI.ViewModels.CharacterDetailsViewModels;
using GIMI_ModManager.WinUI.ViewModels.CharacterGalleryViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

namespace GIMI_ModManager.WinUI.Services;

// For more information on navigation between pages see
// https://github.com/microsoft/TemplateStudio/blob/main/docs/WinUI/navigation.md
public class NavigationService : INavigationService
{
    private readonly ILocalSettingsService _localSettingsService;
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

    public NavigationService(IPageService pageService, ILocalSettingsService localSettingsService)
    {
        _pageService = pageService;
        _localSettingsService = localSettingsService;
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

    public bool NavigateTo(string pageKey, object? parameter = null, bool clearNavigation = false,
        NavigationTransitionInfo? transitionInfo = null)
    {
        var pageType = _pageService.GetPageType(pageKey);

        if (_frame != null && (_frame.Content?.GetType() != pageType ||
                               (parameter != null && !parameter.Equals(_lastParameterUsed))))
        {
            _frame.Tag = clearNavigation;
            var vmBeforeNavigation = _frame.GetPageViewModel();
            var navigated = transitionInfo is null
                ? _frame.Navigate(pageType, parameter)
                : _frame.Navigate(pageType, parameter, transitionInfo);

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


    public bool NavigateToCharacterDetails(string internalName, bool clearNavigation = false)
    {
        var settings = _localSettingsService.ReadSetting<CharacterDetailsSettings>(CharacterDetailsSettings.Key, SettingScope.App);

        if (settings?.GalleryView == true)
        {
            return NavigateTo(typeof(CharacterGalleryViewModel).FullName!, internalName, clearNavigation: clearNavigation);
        }

        var pageKey = typeof(CharacterDetailsViewModel).FullName;

        return NavigateTo(pageKey!, internalName, clearNavigation);
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
                    //GC.Collect();
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

    public void SetListDataItemForNextConnectedAnimation(object item)
    {
        if (item != null!) // Trying to fix an argument null exception
            Frame.SetListDataItemForNextConnectedAnimation(item);
    }

    public void ClearBackStack(int amountToClear = -1, bool clearFromMostRecent = true)
    {
        if (Frame == null)
            return;


        if (amountToClear == -1)
        {
            amountToClear = Frame.BackStack.Count;
        }


        if (clearFromMostRecent)
        {
            for (int i = 0; i < amountToClear; i++)
            {
                Frame.BackStack.RemoveAt(Frame.BackStack.Count - 1);
            }
        }
        else
        {
            for (int i = 0; i < amountToClear; i++)
            {
                Frame.BackStack.RemoveAt(0);
            }
        }
    }

    // Get BackStackItems from Frame

    public ICollection<PageStackEntry> GetBackStackItems() => Frame?.BackStack ?? Array.Empty<PageStackEntry>();
    public IReadOnlyCollection<NavigationHistoryItem> GetNavigationHistory() => _navigationHistory.ToArray();
}