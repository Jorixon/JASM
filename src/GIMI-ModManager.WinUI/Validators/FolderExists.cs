using FluentValidation;
using GIMI_ModManager.WinUI.Services;
using PathPicker = GIMI_ModManager.WinUI.ViewModels.SubVms.PathPicker;

namespace GIMI_ModManager.WinUI.Validators;

public class FolderExists : AbstractValidator<PathPicker>
{
    public FolderExists(string message = "Folder does not exist", bool warning = false)
    {
        RuleFor(x => x.Path).Must(Directory.Exists)
            .WithMessage(message)
            .WithSeverity(warning ? Severity.Warning : Severity.Error);
    }
}