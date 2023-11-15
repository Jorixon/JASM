using FluentValidation;
using GIMI_ModManager.WinUI.ViewModels.SubVms;

namespace GIMI_ModManager.WinUI.Validators.PreConfigured;

public static class GimiFolderRootValidators
{
    public static ICollection<AbstractValidator<PathPicker>> Validators => new AbstractValidator<PathPicker>[]
    {
        new FolderExists(),
        new ContainsAnyFileSystemEntryWithNames(new[] { "3DMigoto Loader.exe", "3DMigotoLoader.exe" })
    };
}