using System.Text.Json.Serialization;
using GIMI_ModManager.Core.Services.GameBanana.Models;
using GIMI_ModManager.WinUI.Services.ModHandling;

namespace GIMI_ModManager.WinUI.Services.Notifications;

[Obsolete("Replaced by ModNotificationsRoot, this file was saved to local settings")]
public class ModNotificationsRootLegacy
{
    public string? Version { get; set; } = null;
    public LegacyModNotification[] ModNotifications { get; set; } = [];


    public ModNotificationsRoot ConvertToModNotificationsRoot()
    {
        var convertedNotifications = ModNotifications.Select(x => new ModNotification
        {
            Time = x.Time,
            Id = x.Id,
            ModId = x.ModId,
            CharacterInternalName = x.CharacterInternalName,
            ModCustomName = x.ModCustomName,
            ModFolderName = x.ModFolderName,
            ShowOnOverview = x.ShowOnOverview,
            AttentionType = x.AttentionType,
            Message = x.Message,
            IsPersistent = x.IsPersistent,
            ModsRetrievedResult = x.ModsRetrievedResult?.ConvertToModsRetrievedResult()
        }).ToArray();


        return new ModNotificationsRoot("1.0") { ModNotifications = convertedNotifications };
    }
}

[Obsolete("Replaced by ModNotification, this file was saved to local settings")]
public sealed class LegacyModNotification
{
    public DateTime Time { get; init; } = DateTime.Now;
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ModId { get; init; }
    public string CharacterInternalName { get; init; } = string.Empty;
    public string ModCustomName { get; init; } = string.Empty;
    public string ModFolderName { get; init; } = string.Empty;
    public bool ShowOnOverview { get; init; }
    public AttentionType AttentionType { get; init; }
    public string Message { get; init; } = string.Empty;
    [JsonIgnore] public bool IsPersistent { get; set; }


    public ModsRetrievedResultLegacy? ModsRetrievedResult { get; init; }


    public record ModsRetrievedResultLegacy
    {
        private string _modId = "-1";

        public string ModId
        {
            get
            {
                if (_modId == "-1" && SitePageUrl != null)
                    return SitePageUrl.Segments.Last();

                return _modId;
            }
            set => _modId = value;
        }

        public DateTime CheckTime { get; init; }
        public DateTime LastCheck { get; init; }
        public Uri SitePageUrl { get; init; } = null!;
        public bool AnyNewMods { get; init; }
        public ICollection<UpdateCheckResultLegacy> Mods { get; init; } = Array.Empty<UpdateCheckResultLegacy>();


        public ModsRetrievedResult ConvertToModsRetrievedResult()
        {
            var mods = Mods.Select(x =>
                    new ModFileInfo(modId: ModId, fileId: string.Empty, fileName: x.FileName,
                        description: x.Description,
                        md5Checksum: x.Md5Checksum, dateAdded: x.DateAdded))
                .ToList();

            return new ModsRetrievedResult
            {
                ModId = ModId,
                CheckTime = CheckTime,
                LastCheck = LastCheck,
                SitePageUrl = SitePageUrl,
                ModFiles = mods
            };
        }
    }

    public record UpdateCheckResultLegacy
    {
        public UpdateCheckResultLegacy(bool isNewer, string fileName, string description, DateTime dateAdded,
            string md5Checksum)
        {
            IsNewer = isNewer;
            FileName = fileName;
            Description = description;
            DateAdded = dateAdded;
            Md5Checksum = md5Checksum;
        }

        public bool IsNewer { get; set; }
        public string FileName { get; }
        public string Description { get; }
        public DateTime DateAdded { get; }
        public TimeSpan Age => DateTime.Now - DateAdded;
        public string Md5Checksum { get; }
    }
}