using System.Reflection;
using GIMI_ModManager.Core.Helpers;

namespace GIMI_ModManager.Core.GamesService.Requests;

public class EditCustomCharacterRequest
{
    public NewValue<string> DisplayName { get; set; }

    public NewValue<bool> IsMultiMod { get; set; }

    public NewValue<Uri?> Image { get; set; }

    public NewValue<string[]> Keys { get; set; }


    public bool AnyValuesSet => GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.PropertyType.IsAssignableTo(typeof(ISettableProperty)))
        .Any(p => (p.GetValue(this) as ISettableProperty)?.IsSet == true);
}