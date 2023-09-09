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

    protected bool Equals(SkinModKeySwapModel other)
    {
        return Condition == other.Condition && ForwardHotkey == other.ForwardHotkey &&
               BackwardHotkey == other.BackwardHotkey && Type == other.Type && Equals(SwapVar, other.SwapVar);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((SkinModKeySwapModel)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Condition, ForwardHotkey, BackwardHotkey, Type, SwapVar);
    }
}