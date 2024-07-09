using FluentValidation;
using GIMI_ModManager.WinUI.ViewModels.SubVms;

namespace GIMI_ModManager.WinUI.Validators.PreConfigured;

public static class GimiFolderRootValidators
{
    public static ICollection<AbstractValidator<PathPicker>> Validators(IEnumerable<string> validMiExeFilenames)
    {
        return new AbstractValidator<PathPicker>[]
        {
            new FolderExists(),
            new ContainsAnyFileSystemEntryWithNames(validMiExeFilenames, warning: true)
        };
    }
}