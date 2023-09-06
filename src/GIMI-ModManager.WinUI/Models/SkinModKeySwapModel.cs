using CommunityToolkit.Mvvm.ComponentModel;
using GIMI_ModManager.Core.Entities;

namespace GIMI_ModManager.WinUI.Models;

public partial class SkinModKeySwapModel : ObservableObject
{
    [ObservableProperty] private string? _condition;
    [ObservableProperty] private string? _forwardHotkey;
    [ObservableProperty] private string? _backwardHotkey;
    [ObservableProperty] private string? _type;
    [ObservableProperty] private string[]? _swapVar;

    public static SkinModKeySwapModel FromKeySwapSettings(SkinModKeySwap skinSwapSetting)
    {
        return new SkinModKeySwapModel
        {
            Condition = skinSwapSetting.Condition,
            ForwardHotkey = skinSwapSetting.ForwardHotkey,
            BackwardHotkey = skinSwapSetting.BackwardHotkey,
            Type = skinSwapSetting.Type,
            SwapVar = skinSwapSetting.SwapVar
        };
    }

    public static SkinModKeySwapModel[] FromKeySwapSettings(SkinModKeySwap[] skinSwapSettings)
        => skinSwapSettings.Select(FromKeySwapSettings).ToArray();

    public SkinModKeySwap ToKeySwapSettings()
    {
        return new SkinModKeySwap
        {
            Condition = Condition,
            ForwardHotkey = ForwardHotkey,
            BackwardHotkey = BackwardHotkey,
            Type = Type,
            SwapVar = SwapVar
        };
    }
}