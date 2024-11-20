using System.Diagnostics.CodeAnalysis;
using GIMI_ModManager.Core.Services.CommandService.Models;
using GIMI_ModManager.WinUI.Services.AppManagement;
using Microsoft.UI.Xaml.Controls;

namespace GIMI_ModManager.WinUI.Views.Settings;

public sealed partial class CreateCommandView : UserControl, IClosableElement
{
    public ViewModels.SettingsViewModels.CreateCommandViewModel ViewModel { get; } =
        App.GetService<ViewModels.SettingsViewModels.CreateCommandViewModel>();

    public event EventHandler? CloseRequested;

    public CreateCommandView(CreateCommandOptions? options = null)
    {
        InitializeComponent();
        Loaded += async (_, _) => await ViewModel.Initialize(options).ConfigureAwait(false);
        ViewModel.CloseRequested += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}

public class CreateCommandOptions
{
    [MemberNotNullWhen(true, nameof(CommandDefinition))]
    public bool IsEditingCommand => CommandDefinition is not null;

    public CommandDefinition? CommandDefinition { get; private set; }


    public bool GameStartCommand { get; private set; }

    public bool GameModelImporterCommand { get; private set; }


    private CreateCommandOptions()
    {
    }


    public static CreateCommandOptions EditCommand(CommandDefinition existingCommandDefinition)
    {
        return new CreateCommandOptions()
        {
            CommandDefinition = existingCommandDefinition
        };
    }


    public static CreateCommandOptions CreateGameCommand()
    {
        return new CreateCommandOptions()
        {
            GameStartCommand = true
        };
    }

    public static CreateCommandOptions CreateModelImporterCommand()
    {
        return new CreateCommandOptions()
        {
            GameModelImporterCommand = true
        };
    }
}