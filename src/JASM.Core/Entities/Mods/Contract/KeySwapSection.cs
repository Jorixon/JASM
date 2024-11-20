using GIMI_ModManager.Core.Entities.Mods.FileModels;

namespace GIMI_ModManager.Core.Entities.Mods.Contract;

public record KeySwapSection
{
    public string SectionName { get; init; } = "Unknown";

    public string? ForwardKey { get; init; }

    public string? BackwardKey { get; init; }

    public int? Variants { get; init; }

    public string Type { get; init; } = "Unknown";


    internal static KeySwapSection FromIniKeySwapSection(IniKeySwapSection iniKeySwapSection)
    {
        return new KeySwapSection
        {
            SectionName = iniKeySwapSection.SectionKey,
            ForwardKey = iniKeySwapSection.ForwardHotkey,
            BackwardKey = iniKeySwapSection.BackwardHotkey,
            Variants = iniKeySwapSection.SwapVar?.Length,
            Type = iniKeySwapSection.Type ?? "Unknown"
        };
    }
}