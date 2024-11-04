using CommunityToolkit.Mvvm.ComponentModel;
using GIMI_ModManager.WinUI.Contracts.ViewModels;

namespace GIMI_ModManager.WinUI.ViewModels;

public partial class DebugViewModel() : ObservableRecipient, INavigationAware
{
    public static bool UseNewModel = true;

    public void OnNavigatedTo(object parameter)
    {
    }

    public void OnNavigatedFrom()
    {
    }
}