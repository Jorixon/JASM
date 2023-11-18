using FluentValidation;
using GIMI_ModManager.WinUI.ViewModels.SubVms;

namespace GIMI_ModManager.WinUI.Validators;

public class ContainsAnyFileSystemEntryWithNames : AbstractValidator<PathPicker>
{
    public ContainsAnyFileSystemEntryWithNames(string[] filenames, string? customMessage = null, bool warning = false)
    {
        filenames = filenames.Select(name => name.ToLower()).ToArray();
        customMessage ??=
            $"Folder does not contain any entry with the specified names: {string.Join(" OR ", filenames)}";

        RuleFor(x => x.Path)
            .Must(path =>
                path is not null &&
                Directory.Exists(path) &&
                Directory.GetFileSystemEntries(path)
                    .Any(entry => filenames.Any(name => entry.ToLower().EndsWith(name)))
            )
            .WithMessage(customMessage)
            .WithSeverity(warning ? Severity.Warning : Severity.Error);
    }
}