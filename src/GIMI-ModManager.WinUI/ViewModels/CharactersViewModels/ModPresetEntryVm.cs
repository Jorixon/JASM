using CommunityToolkit.Mvvm.Input;

namespace GIMI_ModManager.WinUI.ViewModels.CharactersViewModels;

public class ModPresetEntryVm
{
    public string Name { get; set; }

    public IAsyncRelayCommand ApplyPresetCommand { get; set; }

    public ModPresetEntryVm(string name, IAsyncRelayCommand applyPresetCommand)
    {
        Name = name;
        ApplyPresetCommand = applyPresetCommand;
    }
}