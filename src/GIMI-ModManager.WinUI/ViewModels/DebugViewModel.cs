using CommunityToolkit.Mvvm.ComponentModel;
using GIMI_ModManager.WinUI.Contracts.ViewModels;

namespace GIMI_ModManager.WinUI.ViewModels;

public partial class DebugViewModel() : ObservableRecipient, INavigationAware
{
    public void OnNavigatedTo(object parameter)
    {
    }

    public void OnNavigatedFrom()
    {
    }
}