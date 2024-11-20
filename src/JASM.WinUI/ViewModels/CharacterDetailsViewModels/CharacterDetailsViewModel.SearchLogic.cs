using CommunityToolkit.Mvvm.ComponentModel;
using GIMI_ModManager.Core.Helpers;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterDetailsViewModels;

public partial class CharacterDetailsViewModel
{
    [ObservableProperty] private string _searchText = string.Empty;

    public void SearchMods(string queryText)
    {
        queryText = queryText.Trim();

        if (queryText.IsNullOrEmpty())
        {
            ModGridVM.ResetModView();
            AutoSelectFirstMod();
            return;
        }

        var foundMods = ModGridVM.SearchFilterMods(queryText);

        if (foundMods.Length == 0)
            ModGridVM.ClearSelection();
    }
}