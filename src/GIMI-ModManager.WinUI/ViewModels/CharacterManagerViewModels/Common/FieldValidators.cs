namespace GIMI_ModManager.WinUI.ViewModels.CharacterManagerViewModels;

public sealed class FieldValidators<T> : List<ValidationCallback<T>>
{
}

public delegate ValidationResult? ValidationCallback<T>(ValidationContext<T, Form> context);
