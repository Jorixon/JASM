using FluentValidation;
using GIMI_ModManager.WinUI.Services;
using PathPicker = GIMI_ModManager.WinUI.ViewModels.SubVms.PathPicker;

namespace GIMI_ModManager.WinUI.Validators;

public class ContainsFileSystemEntryWithName : AbstractValidator<PathPicker>
{
    public ContainsFileSystemEntryWithName(string filename, string? customMessage = null, bool warning = false)
    {
        filename = filename.ToLower();
        customMessage ??= $"Folder does not contain {filename}";
        RuleFor(x => x.Path)
            .Must(path =>
                path is not null &&
                Directory.Exists(path) &&
                Directory.GetFileSystemEntries(path).Any(entry => entry.ToLower().EndsWith(filename))
            )
            .WithMessage(customMessage)
            .WithSeverity(warning ? Severity.Warning : Severity.Error);
    }
}