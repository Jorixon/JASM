using FluentValidation;
using GIMI_ModManager.WinUI.Services;
using PathPicker = GIMI_ModManager.WinUI.ViewModels.SubVms.PathPicker;

namespace GIMI_ModManager.WinUI.Validators;

public class NotEmpty : AbstractValidator<PathPicker>
{
    public NotEmpty(bool warning = false)
    {
        RuleFor(x => x.Path).NotEmpty().WithMessage("Folder path cannot be empty")
            .WithSeverity(warning ? Severity.Warning : Severity.Error);
    }
}