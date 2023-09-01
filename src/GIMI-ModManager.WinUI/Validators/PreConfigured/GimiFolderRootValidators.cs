using FluentValidation;
using GIMI_ModManager.WinUI.Services;
using PathPicker = GIMI_ModManager.WinUI.ViewModels.SubVms.PathPicker;

namespace GIMI_ModManager.WinUI.Validators.PreConfigured;

public static class GimiFolderRootValidators
{
    public static ICollection<AbstractValidator<PathPicker>> Validators => new AbstractValidator<PathPicker>[]
    {
        new FolderExists(),
        new ContainsFileSystemEntryWithName("3DMigoto Loader.exe")
    };
}