using Newtonsoft.Json;

namespace GIMI_ModManager.WinUI.Models.Options;

public class ProcessOptions : IOptions
{
    [JsonIgnore] public string Key { get; } = "ProcessOptions";
    public string? GenshinExePath { get; set; }
    public string? MigotoExePath { get; set; }
}

public abstract class ProcessOptionsBase : IOptions
{
    [JsonIgnore] public abstract string Key { get; }
    public string? ProcessPath { get; set; }
    public string? WorkingDirectory { get; set; }
}

public class GenshinProcessOptions : ProcessOptionsBase
{
    [JsonIgnore] public const string KeyConst = "GenshinProcessOptions";
    [JsonIgnore] public override string Key { get; } = KeyConst;
}

public class MigotoProcessOptions : ProcessOptionsBase
{
    [JsonIgnore] public const string KeyConst = "MigotoProcessOptions";

    [JsonIgnore] public override string Key { get; } = KeyConst;
}