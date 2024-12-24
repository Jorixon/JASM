using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GIMI_ModManager.WinUI.ViewModels.CharacterManagerViewModels;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public partial class InputField<T> : BaseInputField
{
    public T DefaultValue { get; private set; }

    [ObservableProperty] private T _value;

    public InputField(T value)
    {
        _value = value;
        DefaultValue = value;


        PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(Value) or nameof(DefaultValue))
                IsDirty = !EqualityComparer<T>.Default.Equals(Value, DefaultValue);
        };
    }

    public FieldValidators<T> ValidationRules { get; } = new();

    public override void Validate(Form form)
    {
        ValidationResults.Clear();

        foreach (var rule in ValidationRules)
        {
            if (rule(new ValidationContext<T, Form>(form, this, Value)) is { } result)
            {
                AddValidationResult(result);
            }
        }
    }

    /// <inheritdoc cref="ReInitializeInput(T)"/>
    public void ReInitializeInput(T value, T defaultValue)
    {
        Value = value;
        DefaultValue = defaultValue;
        ValidationResults.Clear();
        IsDirty = false;
    }

    /// <summary>
    /// ReInitializes the input field with the same value and default value. Clears validation results and sets IsDirty to false.
    /// </summary>
    public void ReInitializeInput(T valueAndDefaultValue) => ReInitializeInput(valueAndDefaultValue, valueAndDefaultValue);

    /// <summary>
    /// Resets the input field to its default value. Clears validation results and sets IsDirty to false.
    /// </summary>
    public override void Reset(Form form)
    {
        Value = DefaultValue;
        Validate(form);
        IsDirty = false;
    }

    private string DebuggerDisplay =>
        $"{nameof(Value)}: {Value} | {nameof(DefaultValue)}: {DefaultValue} | {nameof(IsDirty)}: {IsDirty} | {nameof(IsValid)}: {IsValid} | {nameof(ValidationRules)}.Count: {ValidationRules.Count}";
}