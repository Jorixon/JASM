using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterManagerViewModels;

public abstract partial class BaseInputField : ObservableObject
{
    public string FieldName { get; set; } = string.Empty;

    [ObservableProperty] private bool _isDirty;

    public ObservableCollection<ValidationResult> ValidationResults { get; } = new();

    public IEnumerable<ValidationResult> Errors => ValidationResults.Where(r => r.Type == ValidationType.Error);

    public IEnumerable<ValidationResult> Warnings => ValidationResults.Where(r => r.Type == ValidationType.Warning);

    public IEnumerable<ValidationResult> Information => ValidationResults.Where(r => r.Type == ValidationType.Information);

    public bool IsValid => !Errors.Any();

    public abstract void Validate(Form form);

    public abstract void Reset(Form form);

    protected void AddValidationResult(ValidationResult result) => ValidationResults.Add(result);
}

public sealed class NoOpField : BaseInputField
{
    public override void Validate(Form form)
    {
    }

    public override void Reset(Form form)
    {
    }
}

public readonly struct ValidationContext<TValue, TForm>(TForm form, BaseInputField field, TValue value) where TForm : Form
{
    public TForm Form { get; } = form;

    public BaseInputField Field { get; } = field;

    public TValue Value { get; } = value;
}

public class ValidationResult
{
    public required string Message { get; init; }

    public ValidationType Type { get; init; } = ValidationType.Error;
}

public enum ValidationType
{
    Information,
    Warning,
    Error
}