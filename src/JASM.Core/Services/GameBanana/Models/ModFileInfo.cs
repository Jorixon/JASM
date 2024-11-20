using System.Text.Json.Serialization;
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

    public ModFileInfo(string modId, string fileId, string fileName, string description, string md5Checksum,
        DateTime dateAdded)
    {
        ModId = modId;
        FileId = fileId;
        FileName = fileName;
        Description = description;
        Md5Checksum = md5Checksum;
        DateAdded = dateAdded;
    }

    [JsonConstructor]
    [Obsolete("This constructor is for serialization purposes only.")]
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public ModFileInfo()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    {
    }

    public string ModId { get; init; }
    public string FileId { get; init; }
    public string FileName { get; init; }
    public string Description { get; init; }
    public DateTime DateAdded { get; init; }
    [JsonIgnore] public TimeSpan Age => DateTime.Now - DateAdded;
    public string Md5Checksum { get; init; }
}