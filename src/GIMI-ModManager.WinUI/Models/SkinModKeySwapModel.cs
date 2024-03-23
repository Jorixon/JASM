using CommunityToolkit.Mvvm.ComponentModel;
using GIMI_ModManager.Core.Entities.Mods.Contract;
using GIMI_ModManager.Core.Helpers;

namespace GIMI_ModManager.WinUI.Models;

public partial class SkinModKeySwapModel : ObservableObject, IEquatable<SkinModKeySwapModel>
{
    [ObservableProperty] private string _sectionKey = string.Empty;

    [ObservableProperty] private string? _condition;
    [ObservableProperty] private string? _forwardHotkey;
    [ObservableProperty] private string? _backwardHotkey;
    [ObservableProperty] private string? _type;
    [ObservableProperty] private string _variationsCount = "Unknown";

    public static SkinModKeySwapModel FromKeySwapSettings(KeySwapSection skinSwapSetting)
    {
        return new SkinModKeySwapModel
        {
            SectionKey = skinSwapSetting.SectionName,
            ForwardHotkey = skinSwapSetting.ForwardKey,
            BackwardHotkey = skinSwapSetting.BackwardKey,
            Type = skinSwapSetting.Type,
            VariationsCount = skinSwapSetting.Variants?.ToString() ?? "Unknown"
        };
    }

    public static SkinModKeySwapModel[] FromKeySwapSettings(KeySwapSection[] skinSwapSettings)
        => skinSwapSettings.Select(FromKeySwapSettings).ToArray();


    public bool Equals(SkinModKeySwapModel? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Condition == other.Condition &&
               (ForwardHotkey == other.ForwardHotkey ||
                ForwardHotkey.IsNullOrEmpty() && other.ForwardHotkey.IsNullOrEmpty())
               &&
               (BackwardHotkey == other.BackwardHotkey ||
                BackwardHotkey.IsNullOrEmpty() && other.BackwardHotkey.IsNullOrEmpty()) &&
               Type == other.Type;
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is SkinModKeySwapModel other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Condition, ForwardHotkey, BackwardHotkey, Type);
    }

    public static IEqualityComparer<SkinModKeySwapModel> EqualityComparer { get; } =
        new SkinModKeySwapModelEqualityComparer();

    public class SkinModKeySwapModelEqualityComparer : IEqualityComparer<SkinModKeySwapModel>
    {
        public bool Equals(SkinModKeySwapModel? x, SkinModKeySwapModel? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;
            return x.Equals(y);
        }

        public int GetHashCode(SkinModKeySwapModel obj)
        {
            return obj.GetHashCode();
        }
    }
}