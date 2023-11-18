#nullable enable
using System.Diagnostics;
using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Entities.Mods.SkinMod;
using GIMI_ModManager.Core.GamesService.Interfaces;
using GIMI_ModManager.Core.Helpers;
using Serilog;

namespace GIMI_ModManager.Core.Entities;

public sealed class CharacterModList : ICharacterModList
{
    private readonly ILogger? _logger;

    public IReadOnlyCollection<CharacterSkinEntry> Mods
    {
        get
        {
            lock (_modsLock)
                return new List<CharacterSkinEntry>(_mods).AsReadOnly();
        }
    }

    public string AbsModsFolderPath { get; }
    private readonly List<CharacterSkinEntry> _mods = new();
    public const string DISABLED_PREFIX = ModFolderHelpers.DISABLED_PREFIX;
    public const string ALT_DISABLED_PREFIX = ModFolderHelpers.ALT_DISABLED_PREFIX;
    public string DisabledPrefix => DISABLED_PREFIX;
    private readonly FileSystemWatcher _watcher;
    public IModdableObject Character { get; }

    private readonly object _modsLock = new();

    internal CharacterModList(IModdableObject character, string absPath, ILogger? logger = null)
    {
        _logger = logger?.ForContext<CharacterModList>();
        Character = character;
        AbsModsFolderPath = absPath;
        _watcher = new FileSystemWatcher(AbsModsFolderPath);
        _watcher.NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.CreationTime;
        _watcher.Renamed += OnModRenamed;
        _watcher.Created += OnModCreated;
        _watcher.Deleted += OnModDeleted;
        _watcher.Error += OnWatcherError;

        _watcher.IncludeSubdirectories = false;
        _watcher.EnableRaisingEvents = true;
    }

    public event EventHandler<ModFolderChangedArgs>? ModsChanged;

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        _logger?.Error(e.GetException(), "Error in FileSystemWatcher");
    }

    private void OnModDeleted(object sender, FileSystemEventArgs e)
    {
        _logger?.Information("Mod {ModName} in {characterFolder} folder was deleted", e.Name, Character.InternalName);
        Task.Run(() =>
        {
            if (_mods.Any(mod => mod.Mod.FullPath == e.FullPath))
            {
                var mod = _mods.First(mod => mod.Mod.FullPath == e.FullPath);

                UnTrackMod(mod.Mod);
            }
            else
            {
                _logger?.Warning("Deleted folder {Folder} was not tracked in mod list", e.FullPath);
            }

            ModsChanged?.Invoke(this, new ModFolderChangedArgs(e.FullPath, ModFolderChangeType.Deleted));
        });
    }

    private void OnModCreated(object sender, FileSystemEventArgs e)
    {
        _logger?.Information("Mod {ModName} was created in {characterFolder} created", e.Name, Character.InternalName);

        Task.Run(async () =>
        {
            ISkinMod newSkinMod = null!;
            try
            {
                newSkinMod = await SkinMod.CreateModAsync(new DirectoryInfo(e.FullPath));
            }
            catch (Exception exception)
            {
                _logger?.Error(exception, "Error initializing mod");
                return;
            }


            if (ModAlreadyAdded(newSkinMod))
                _logger?.Warning("Created folder {Folder} was already tracked in {characterFolder} mod list", e.Name,
                    Character.InternalName);
            else
                TrackMod(newSkinMod);
            ModsChanged?.Invoke(this, new ModFolderChangedArgs(e.FullPath, ModFolderChangeType.Created));
        });
    }

    private void OnModRenamed(object sender, RenamedEventArgs e)
    {
        _logger?.Information("Mod {ModName} renamed to {NewName}", e.OldFullPath, e.FullPath);

        Task.Run(async () =>
        {
            if (_mods.FirstOrDefault(mod => mod.Mod.FullPath == e.OldFullPath) is { } oldModEntry)
            {
                ISkinMod newSkinMod = null!;
                try
                {
                    newSkinMod = await SkinMod.CreateModAsync(new DirectoryInfo(e.FullPath));
                    await newSkinMod.Settings.ReadSettingsAsync();
                }
                catch (Exception exception)
                {
                    _logger?.Error(exception, "Error initializing mod");
                    return;
                }

                var modEntry = new CharacterSkinEntry(newSkinMod, this, IsModFolderEnabled(newSkinMod.Name));
                UnTrackMod(oldModEntry.Mod);
                TrackMod(modEntry.Mod);
            }
            else
            {
                _logger?.Warning("Renamed folder {Folder} was not tracked in mod list", e.OldFullPath);
            }

            ModsChanged?.Invoke(this, new ModFolderChangedArgs(e.FullPath, ModFolderChangeType.Renamed, e.OldFullPath));
        });
    }


    public void TrackMod(ISkinMod mod)
    {
        lock (_modsLock)
        {
            if (ModAlreadyAdded(mod))
            {
                _logger?.Warning("Mod {ModName} was already tracked in {CharacterName} modList", mod.Name,
                    Character.InternalName);
                return;
            }

            _mods.Add(ModFolderHelpers.FolderHasDisabledPrefix(mod.Name)
                ? new CharacterSkinEntry(mod, this, false)
                : new CharacterSkinEntry(mod, this, true));
            _logger?.Verbose("Tracking {ModName} in {CharacterName} modList", mod.Name, Character.InternalName);
            Debug.Assert(_mods.DistinctBy(m => m.Id).Count() == _mods.Count);
        }
    }

    // Untrack
    public void UnTrackMod(ISkinMod mod)
    {
        lock (_modsLock)
        {
            if (!ModAlreadyAdded(mod))
            {
                _logger?.Warning("Mod {ModName} was not tracked in {CharacterName} modList", mod.Name,
                    Character.DisplayName);
                return;
            }

            _mods.Remove(_mods.First(m => m.Mod.Equals(mod)));

            _logger?.Debug("Stopped tracking {ModName} in {CharacterName} modList", mod.Name, Character.InternalName);
            Debug.Assert(_mods.DistinctBy(m => m.Id).Count() == _mods.Count);
        }
    }

    public void EnableMod(Guid modId)
    {
        try
        {
            _watcher.EnableRaisingEvents = false;

            var mod = _mods.First(m => m.Id == modId).Mod;

            if (!ModAlreadyAdded(mod))
                throw new InvalidOperationException("Mod not added");

            if (!ModFolderHelpers.FolderHasDisabledPrefix(mod.Name))
                throw new InvalidOperationException("Cannot enable a enabled mod");

            var newName = ModFolderHelpers.GetFolderNameWithoutDisabledPrefix(mod.Name);

            if (Directory.Exists(Path.Combine(AbsModsFolderPath, newName)))
                throw new InvalidOperationException("Cannot disable a mod with the same name as a disabled mod");

            mod.Rename(newName);


            _mods.First(m => m.Mod == mod).IsEnabled = true;
        }
        finally
        {
            _watcher.EnableRaisingEvents = true;
        }
    }

    public void DisableMod(Guid modId)
    {
        try
        {
            _watcher.EnableRaisingEvents = false;
            var mod = _mods.First(m => m.Id == modId).Mod;

            if (!ModAlreadyAdded(mod))
                throw new InvalidOperationException("Mod not added");

            if (ModFolderHelpers.FolderHasDisabledPrefix(mod.Name))
                throw new InvalidOperationException("Cannot disable a disabled mod");

            var newName = ModFolderHelpers.GetFolderNameWithDisabledPrefix(mod.Name);

            if (Directory.Exists(Path.Combine(AbsModsFolderPath, newName)))
                throw new InvalidOperationException("Cannot disable a mod with the same name as a disabled mod");

            mod.Rename(newName);
            _mods.First(m => m.Mod.Equals(mod)).IsEnabled = false;
        }
        finally
        {
            _watcher.EnableRaisingEvents = true;
        }
    }

    public bool IsModEnabled(ISkinMod mod)
    {
        if (!ModAlreadyAdded(mod))
            throw new InvalidOperationException("Mod not added");

        return _mods.First(m => m.Mod.Equals(mod)).IsEnabled;
    }

    private bool ModAlreadyAdded(ISkinMod mod)
    {
        return _mods.Any(m => m.Mod.Equals(mod));
    }

    public void Dispose()
    {
        _watcher.Dispose();
    }

    public override string ToString()
    {
        return $"{Character} ({Mods.Count} mods)";
    }

    public DisableWatcher DisableWatcher()
    {
        return new DisableWatcher(_watcher);
    }

    public bool FolderAlreadyExists(string folderName)
    {
        if (Path.IsPathFullyQualified(folderName))
            folderName = Path.GetDirectoryName(folderName) ?? folderName;

        var enabledFolderNamePath = Path.Combine(AbsModsFolderPath,
            ModFolderHelpers.GetFolderNameWithoutDisabledPrefix(folderName));
        var disabledFolderNamePath =
            Path.Combine(AbsModsFolderPath, ModFolderHelpers.GetFolderNameWithDisabledPrefix(folderName));

        return Directory.Exists(enabledFolderNamePath) || Directory.Exists(disabledFolderNamePath);
    }


    public void DeleteModBySkinEntryId(Guid skinEntryId, bool moveToRecycleBin = true)
    {
        lock (_modsLock)
        {
            var skinEntry = _mods.FirstOrDefault(modEntry => modEntry.Id == skinEntryId);
            if (skinEntry is null)
                throw new InvalidOperationException("Skin entry not found");
            using var disableWatcher = DisableWatcher();
            var mod = skinEntry.Mod;

            UnTrackMod(skinEntry.Mod);
            mod.Delete(moveToRecycleBin);
            _logger?.Information("{Operation} mod {ModName} from {CharacterName} modList",
                moveToRecycleBin ? "Recycled" : "Deleted", mod.Name, Character.InternalName);
        }
    }

    public bool IsMultipleModsActive(bool perSkin = false)
    {
        return _mods.Count(mod => mod.IsEnabled) > 1;
    }

    private static bool IsModFolderEnabled(string folderName)
    {
        return !IsModFolderDisabled(folderName);
    }

    private static bool IsModFolderDisabled(string folderName)
    {
        return folderName.StartsWith(DISABLED_PREFIX, StringComparison.CurrentCultureIgnoreCase) ||
               folderName.StartsWith(ALT_DISABLED_PREFIX, StringComparison.CurrentCultureIgnoreCase);
    }
}

public class ModFolderChangedArgs : EventArgs
{
    public ModFolderChangedArgs(string newName, ModFolderChangeType changeType, string? oldName = null)
    {
        if (changeType == ModFolderChangeType.Renamed && oldName is null)
            throw new ArgumentException("Old name must be provided when change type is renamed", nameof(oldName));

        ArgumentNullException.ThrowIfNull(newName);

        NewName = newName;
        ChangeType = changeType;
        OldName = oldName;
    }

    public string NewName { get; }
    public string? OldName { get; }
    public ModFolderChangeType ChangeType { get; }
}

public enum ModFolderChangeType
{
    Created,
    Deleted,
    Renamed
}

public class DisableWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;

    public DisableWatcher(FileSystemWatcher watcher)
    {
        _watcher = watcher;
        _watcher.EnableRaisingEvents = false;
    }

    public void Dispose()
    {
        _watcher.EnableRaisingEvents = true;
    }
}