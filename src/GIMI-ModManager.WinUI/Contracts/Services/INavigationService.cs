using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace GIMI_ModManager.WinUI.Contracts.Services;

public interface INavigationService
{
    event NavigatedEventHandler Navigated;
    bool CanGoForward { get; }
    bool CanGoBack { get; }

    Frame? Frame { get; set; }

    bool NavigateTo(string pageKey, object? parameter = null, bool clearNavigation = false);
    bool GoForward();

    bool GoBack();
    void SetListDataItemForNextConnectedAnimation(object item);

    public ICollection<PageStackEntry> GetBackStackItems();
    public IReadOnlyCollection<NavigationHistoryItem> GetNavigationHistory();
}

public record NavigationHistoryItem
{
    public NavigationHistoryItem(Type PageType, object? Parameter)
    {
        this.PageType = PageType;
        this.Parameter = Parameter;
    }

    public NavigationHistoryItem(PageStackEntry pageStackEntry)
    {
        PageType = pageStackEntry.SourcePageType;
        Parameter = pageStackEntry.Parameter;
    }

    public Type PageType { get; init; }
    public object? Parameter { get; init; }

    public void Deconstruct(out Type PageType, out object? Parameter)
    {
        PageType = this.PageType;
        Parameter = this.Parameter;
    }
}