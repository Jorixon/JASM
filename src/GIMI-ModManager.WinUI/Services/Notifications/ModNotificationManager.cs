using GIMI_ModManager.Core.Entities.Genshin;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Models.Options;
using Serilog;

namespace GIMI_ModManager.WinUI.Services.Notifications;

public class ModNotificationManager
{
    private readonly ILogger _logger;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly List<IModNotification> _inMemoryModNotifications = new();
    public IReadOnlyCollection<IModNotification> InMemoryModNotifications => _inMemoryModNotifications.AsReadOnly();

    public event EventHandler<ModNotificationEvent>? ModNotificationAdded;
    public event EventHandler<ModNotificationEvent[]>? ModNotificationsCleared;

    public ModNotificationManager(ILogger logger, ILocalSettingsService localSettingsService)
    {
        _logger = logger;
        _localSettingsService = localSettingsService;
    }

    public async Task AddModNotification(IModNotification modNotification, bool persistent = false)
    {
        if (!persistent)
        {
            _inMemoryModNotifications.Add(modNotification);
            ModNotificationAdded?.Invoke(this, new ModNotificationEvent(modNotification, false));
            return;
        }


        var modAttentionSettings =
            await _localSettingsService.ReadOrCreateSettingAsync<ModAttentionSettings>(ModAttentionSettings.Key);

        if (modAttentionSettings.ModNotifications.TryGetValue(modNotification.CharacterId, out var notifications))
        {
            modAttentionSettings.ModNotifications.Remove(modNotification.CharacterId);
            modAttentionSettings.ModNotifications.Add(modNotification.CharacterId,
                notifications.Append(modNotification).ToArray());
        }

        modAttentionSettings.ModNotifications.Add(modNotification.CharacterId,
            new IModNotification[] { modNotification });

        await _localSettingsService.SaveSettingAsync(ModAttentionSettings.Key, modAttentionSettings)
            .ConfigureAwait(false);

        ModNotificationAdded?.Invoke(this, new ModNotificationEvent(modNotification, true));
    }

    public async Task<IModNotification[]> GetPersistentModNotifications(GenshinCharacter? genshinCharacter = null)
    {
        var modAttentionSettings =
            await _localSettingsService.ReadOrCreateSettingAsync<ModAttentionSettings>(ModAttentionSettings.Key);

        return genshinCharacter is null
            ? modAttentionSettings.ModNotifications.Select(x => x.Value).SelectMany(x => x).ToArray()
            : modAttentionSettings.ModNotifications.First(x => x.Key == genshinCharacter.Id).Value;
    }

    public IModNotification[] GetInMemoryModNotifications(GenshinCharacter? genshinCharacter = null)
    {
        return genshinCharacter is null
            ? _inMemoryModNotifications.ToArray()
            : _inMemoryModNotifications.Where(x => x.CharacterId == genshinCharacter.Id).ToArray();
    }

    public async Task ClearModNotifications(GenshinCharacter? genshinCharacter = null, bool persistent = false)
    {
        if (!persistent)
        {
            var removedNotifications = genshinCharacter is null
                ? _inMemoryModNotifications.ToArray()
                : _inMemoryModNotifications.Where(x => x.CharacterId == genshinCharacter.Id).ToArray();
            if (genshinCharacter is null)
                _inMemoryModNotifications.Clear();

            else
                _inMemoryModNotifications.RemoveAll(x => x.CharacterId == genshinCharacter.Id);


            ModNotificationsCleared?.Invoke(this, removedNotifications.Select(x => new ModNotificationEvent(x, false))
                .ToArray());

            return;
        }

        var modAttentionSettings =
            await _localSettingsService.ReadOrCreateSettingAsync<ModAttentionSettings>(ModAttentionSettings.Key);

        var removedPersistentNotifications = genshinCharacter is null
            ? modAttentionSettings.ModNotifications.Select(x => x.Value).SelectMany(x => x).ToArray()
            : modAttentionSettings.ModNotifications.First(x => x.Key == genshinCharacter.Id).Value;

        if (genshinCharacter is null)
            modAttentionSettings.ModNotifications.Clear();
        else
            modAttentionSettings.ModNotifications.Remove(genshinCharacter.Id);

        await _localSettingsService.SaveSettingAsync(ModAttentionSettings.Key, modAttentionSettings)
            .ConfigureAwait(false);

        ModNotificationsCleared?.Invoke(this, removedPersistentNotifications
            .Select(x => new ModNotificationEvent(x, true))
            .ToArray());
    }

    public async Task<bool> RemoveModNotification(Guid notificationId, bool persistent = false)
    {
        if (!persistent)
        {
            var removedNotification = _inMemoryModNotifications.FirstOrDefault(x => x.Id == notificationId);
            if (removedNotification is null)
                return false;

            _inMemoryModNotifications.Remove(removedNotification);
            ModNotificationsCleared?.Invoke(this, new ModNotificationEvent[1]
            {
                new(removedNotification, false)
            });
            return true;
        }

        var modAttentionSettings =
            await _localSettingsService.ReadOrCreateSettingAsync<ModAttentionSettings>(ModAttentionSettings.Key);

        var removedPersistentNotification = modAttentionSettings.ModNotifications.Select(x => x.Value)
            .SelectMany(x => x).FirstOrDefault(x => x.Id == notificationId);

        if (removedPersistentNotification is null)
            return false;

        modAttentionSettings.ModNotifications.Select(x => x.Value).SelectMany(x => x)
            .Where(x => x.Id == notificationId).ToList().ForEach(x =>
            {
                modAttentionSettings.ModNotifications.Select(x => x.Value).SelectMany(x => x).ToList().Remove(x);
            });

        await _localSettingsService.SaveSettingAsync(ModAttentionSettings.Key, modAttentionSettings)
            .ConfigureAwait(false);

        ModNotificationsCleared?.Invoke(this, new ModNotificationEvent[]
        {
            new(removedPersistentNotification, true)
        });
        return true;
    }


    public class ModNotificationEvent : EventArgs
    {
        public IModNotification ModNotification { get; }
        public bool IsPersistent { get; }

        public ModNotificationEvent(IModNotification modNotification, bool isPersistent)
        {
            ModNotification = modNotification;
            IsPersistent = isPersistent;
        }
    }
}