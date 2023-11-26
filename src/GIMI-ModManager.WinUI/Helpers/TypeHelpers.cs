namespace GIMI_ModManager.WinUI.Helpers;

public static class TypeHelpers
{
    public static bool InheritsFrom(this object type, Type baseType)
    {
        ArgumentNullException.ThrowIfNull(type, nameof(type));
        ArgumentNullException.ThrowIfNull(baseType, nameof(baseType));

        return baseType.IsInstanceOfType(type);
    }
}