using System.Reflection;
using GIMI_ModManager.Core.GamesService.JsonModels;
using GIMI_ModManager.Core.Helpers;
using Newtonsoft.Json;

var toolDir = new DirectoryInfo(Assembly.GetEntryAssembly()!.Location + @"..\..\..\..\..\..\");

Console.WriteLine(toolDir.FullName);

var jsonPath = $"{toolDir.FullName}\\characters.json";
var jsonNewPath = $"{toolDir.FullName}\\charactersNew.json";


var json = File.ReadAllText(jsonPath);

var characters = JsonConvert.DeserializeObject<List<JsonOverride>>(json);


characters!.ForEach(c =>
{
    c.Image = null;
    c.Keys = null;
    (c.InGameSkins ?? []).ForEach(s => { s.Image = null; });

    if (c.InGameSkins != null && c.InGameSkins.Count == 0)
    {
        c.InGameSkins = null;
    }
});


File.WriteAllText(jsonNewPath, JsonConvert.SerializeObject(characters, Formatting.Indented));