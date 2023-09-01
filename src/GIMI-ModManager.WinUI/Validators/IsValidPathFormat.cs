using FluentValidation;
using GIMI_ModManager.WinUI.Services;
using PathPicker = GIMI_ModManager.WinUI.ViewModels.SubVms.PathPicker;

namespace GIMI_ModManager.WinUI.Validators;

public class IsValidPathFormat : AbstractValidator<PathPicker>
{
    public IsValidPathFormat(bool warning = false)
    {
        RuleFor(x => x.Path)
            .Must(path => path is not null && Path.IsPathFullyQualified(path))
            .WithMessage("Path is not valid")
            .WithSeverity(warning ? Severity.Warning : Severity.Error);
    }
}