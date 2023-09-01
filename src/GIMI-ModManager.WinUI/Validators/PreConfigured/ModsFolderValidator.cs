using FluentValidation;
using GIMI_ModManager.WinUI.Services;
using PathPicker = GIMI_ModManager.WinUI.ViewModels.SubVms.PathPicker;

namespace GIMI_ModManager.WinUI.Validators.PreConfigured;

public static class ModsFolderValidator
{
    public static IEnumerable<AbstractValidator<PathPicker>> Validators => new AbstractValidator<PathPicker>[]
    {
        new IsValidPathFormat(),
        new FolderExists("Folder not found")
    };
}