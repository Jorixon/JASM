using GIMI_ModManager.Core.Helpers;

namespace GIMI_ModManager.Core.GamesService.Requests;

public class EditCustomCharacterRequest
{
    public NewValue<string> DisplayName { get; set; }


    public bool AnyValuesSet => DisplayName.IsSet;
}