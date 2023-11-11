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
}