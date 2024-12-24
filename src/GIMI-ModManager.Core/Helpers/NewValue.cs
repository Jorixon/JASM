using System.Diagnostics.CodeAnalysis;

namespace GIMI_ModManager.Core.Helpers;

public readonly struct NewValue<T>
{
    private NewValue(T valueToSet)
    {
        ValueToSet = valueToSet;
        IsSet = true;
    }

    public bool IsSet { get; }
    public T ValueToSet { get; }

    public static implicit operator T(NewValue<T> newValue) => newValue.ValueToSet;

    public static NewValue<T> Set(T value) => new(value);
}

public static class NewValueExtensions
{
    public static NewValue<string?>? EmptyStringToNull([NotNullIfNotNull(nameof(newValue))] this NewValue<string?>? newValue)
    {
        if (newValue is null)
            return null;

        return string.IsNullOrWhiteSpace(newValue.Value) ? NewValue<string?>.Set(null) : newValue;
    }
}