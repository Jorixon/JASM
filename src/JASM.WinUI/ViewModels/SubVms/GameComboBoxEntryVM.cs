using GIMI_ModManager.Core.GamesService;

namespace GIMI_ModManager.WinUI.ViewModels.SubVms;

public class GameComboBoxEntryVM(SupportedGames value)
{
    public SupportedGames Value { get; } = value;
    public required string GameName { get; init; }
    public required string GameShortName { get; init; }
    public required Uri GameIconPath { get; init; }
}