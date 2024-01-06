#nullable enable
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
    private FileSystemWatcher? _watcher;

    private readonly FileSystemWatcher _selfWatcher;

    public IModdableObject Character { get; }

    private readonly object _modsLock = new();

    [MemberNotNullWhen(true, nameof(_watcher))]
    public bool IsCharacterFolderCreated()
    {
        var modFolderExists = Directory.Exists(AbsModsFolderPath);

        if (modFolderExists && _watcher is null)
            throw new InvalidOperationException("mod Watcher is null when mod folder exists");

        if (!modFolderExists && _watcher is not null)
            throw new InvalidOperationException("mod Watcher is not null when mod folder does not exist");

        return modFolderExists;
    }

    internal CharacterModList(IModdableObject character, string absPath, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(absPath);


        _logger = logger?.ForContext<CharacterModList>();
        Character = character;
        AbsModsFolderPath = absPath;

        _selfWatcher = new FileSystemWatcher(Directory.GetParent(absPath)!.FullName);
        _selfWatcher.NotifyFilter = NotifyFilters.DirectoryName;
        _selfWatcher.Created += OnSelfFolderCreated;
        _selfWatcher.Deleted += OnSelfFolderDeleted;
        _selfWatcher.Error += OnWatcherError;

        _selfWatcher.EnableRaisingEvents = true;

        if (!Directory.Exists(AbsModsFolderPath))
            return;


        _watcher = CreateModWatcher();
    }

    public event EventHandler<ModFolderChangedArgs>? ModsChanged;

    private void OnSelfFolderCreated(object sender, FileSystemEventArgs e)
    {
        if (!Character.InternalNameEquals(e.Name))
            return;

        if (_watcher is not null)
            throw new InvalidOperationException("Watcher is not null when mod folder is created");

        _watcher = CreateModWatcher();
    }


    private void OnSelfFolderDeleted(object sender, FileSystemEventArgs e)
    {
        if (!Character.InternalNameEquals(e.Name))
            return;

        if (_watcher is null)
            throw new InvalidOperationException("Self watcher is null when mod folder is deleted");

        var oldModWatcher = _watcher;
        _watcher = null;
        oldModWatcher?.Dispose();
    }


    private FileSystemWatcher CreateModWatcher()
    {
        var watcher = new FileSystemWatcher(AbsModsFolderPath);
        watcher.NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.CreationTime;
        watcher.Renamed += OnModRenamed;
        watcher.Created += OnModCreated;
        watcher.Deleted += OnModDeleted;
        watcher.Error += OnWatcherError;

        watcher.IncludeSubdirectories = false;
        watcher.EnableRaisingEvents = true;
        return watcher;
    }


    public void InstantiateCharacterFolder()
    {
        lock (_modsLock)
        {
            if (IsCharacterFolderCreated())
                return;

            using var disableSelfWatcher = new DisableWatcher(_selfWatcher);
            Directory.CreateDirectory(AbsModsFolderPath);
            _watcher = CreateModWatcher();
        }

        _logger?.Debug("Created {CharacterName} mod folder", Character.InternalName);
    }

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
                newSkinMod = await SkinMod.CreateModAsync(new DirectoryInfo(e.FullPath), true);
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

            _logger?.Verbose("Stopped tracking {ModName} in {CharacterName} modList", mod.Name, Character.InternalName);
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


            _mods.First(m => m.Mod.Equals(mod)).IsEnabled = true;
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

    public void RenameMod(ISkinMod mod, string newName)
    {
        lock (_modsLock)
        {
            if (!ModAlreadyAdded(mod))
                throw new InvalidOperationException("Mod not tracked");

            if (FolderAlreadyExists(newName))
                throw new InvalidOperationException("Cannot rename mod to a name that already exists");

            using var disableWatcher = DisableWatcher();

            if (IsModEnabled(mod))
            {
                var newModName = ModFolderHelpers.GetFolderNameWithoutDisabledPrefix(newName);
                mod.Rename(newModName);
            }
            else
            {
                var newModName = ModFolderHelpers.GetFolderNameWithDisabledPrefix(newName);
                mod.Rename(newModName);
            }
        }
    }

    private bool ModAlreadyAdded(ISkinMod mod)
    {
        return _mods.Any(m => m.Mod.Equals(mod));
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _selfWatcher.Dispose();
        _watcher = null;
    }

    public override string ToString()
    {
        return $"{Character} ({Mods.Count} mods)";
    }

    public DisableWatcher DisableWatcher()
    {
        InstantiateCharacterFolder();
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
        ArgumentNullException.ThrowIfNull(watcher);
        _watcher = watcher;
        _watcher.EnableRaisingEvents = false;
    }

    public void Dispose()
    {
        _watcher.EnableRaisingEvents = true;
    }
}