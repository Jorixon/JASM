using GIMI_ModManager.Core.GamesService.JsonModels;
using Newtonsoft.Json;

namespace GIMI_ModManager.Core.GamesService;

internal class GameSettingsManager
{
    private readonly string _settingsFile;

    public GameSettingsManager(DirectoryInfo settingsDirectory)
    {
        _settingsFile = Path.Combine(settingsDirectory.FullName, "GameService.json");
    }

    public async Task<JsonOverride[]> ReadSettings()
    {
        if (!File.Exists(_settingsFile))
        {
            return Array.Empty<JsonOverride>();
        }

        return JsonConvert.DeserializeObject<JsonOverride[]>(await File.ReadAllTextAsync(_settingsFile)) ??
               Array.Empty<JsonOverride>();
    }

    public void SaveSettings(JsonOverride[] settings)
    {
        File.WriteAllText(_settingsFile, JsonConvert.SerializeObject(settings, Formatting.Indented));
    }
}