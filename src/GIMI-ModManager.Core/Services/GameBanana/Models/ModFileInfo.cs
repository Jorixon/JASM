using GIMI_ModManager.Core.Services.GameBanana.ApiModels;

namespace GIMI_ModManager.Core.Services.GameBanana.Models;

public class ModFileInfo
{
    public ModFileInfo(ApiModFileInfo apiModFileInfo, string modId)
    {
        ModId = modId;
        FileId = apiModFileInfo.FileId.ToString();
        FileName = apiModFileInfo.FileName;
        Description = apiModFileInfo.Description;
        DateAdded = DateTimeOffset.FromUnixTimeSeconds(apiModFileInfo.DateAdded).DateTime;
        Md5Checksum = apiModFileInfo.Md5Checksum;
        ModId = modId;
    }

    public string ModId { get; init; }
    public string FileId { get; init; }
    public string FileName { get; init; }
    public string Description { get; init; }
    public DateTime DateAdded { get; init; }
    public TimeSpan Age => DateTime.Now - DateAdded;
    public string Md5Checksum { get; init; }
}