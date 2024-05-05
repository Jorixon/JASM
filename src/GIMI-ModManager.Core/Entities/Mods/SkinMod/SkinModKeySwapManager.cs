using GIMI_ModManager.Core.Contracts.Entities;
using GIMI_ModManager.Core.Entities.Mods.Contract;
using GIMI_ModManager.Core.Entities.Mods.FileModels;
using GIMI_ModManager.Core.Helpers;
using OneOf;

namespace GIMI_ModManager.Core.Entities.Mods.SkinMod;

public class SkinModKeySwapManager
{
    private readonly ISkinMod _skinMod;
    private List<KeySwapSection>? _keySwaps;

    public SkinModKeySwapManager(ISkinMod skinMod)
    {
        _skinMod = skinMod;
    }

    public void ClearKeySwaps()
    {
        _keySwaps = null;
    }

    private async Task<string> GetIniPathAsync()
    {
        var iniPath = await _skinMod.GetModIniPathAsync().ConfigureAwait(false);
        if (iniPath is null)
            throw new InvalidOperationException("Mod ini could not be found");

        return iniPath;
    }

    public async Task<IReadOnlyList<KeySwapSection>> ReadKeySwapConfiguration(
        CancellationToken cancellationToken = default)
    {
        List<string> keySwapLines = new();
        List<IniKeySwapSection> keySwaps = new();
        var keySwapBlockStarted = false;
        var currentLine = -1;
        var sectionLine = string.Empty;
        await foreach (var line in File.ReadLinesAsync(await GetIniPathAsync().ConfigureAwait(false), cancellationToken)
                           .ConfigureAwait(false))
        {
            currentLine++;
            if (line.Trim().StartsWith(";") || string.IsNullOrWhiteSpace(line))
                continue;

            if (IniConfigHelpers.IsSection(line) && keySwapBlockStarted ||
                keySwapBlockStarted && keySwapLines.Count > 9)
            {
                keySwapBlockStarted = false;

                var keySwap = IniConfigHelpers.ParseKeySwap(keySwapLines, sectionLine);
                if (keySwap is not null)
                    keySwaps.Add(keySwap);
                keySwapLines.Clear();

                if (IniConfigHelpers.IsSection(line))
                {
                    sectionLine = line;
                    keySwapBlockStarted = true;
                }
                else
                {
                    sectionLine = string.Empty;
                }


                continue;
            }

            if (IniConfigHelpers.IsSection(line))
            {
                keySwapBlockStarted = true;
                sectionLine = line;
                continue;
            }

            if (keySwapLines.Count > 10 && !IniConfigHelpers.IsSection(line) &&
                keySwapBlockStarted)
            {
                keySwapBlockStarted = false;
                sectionLine = string.Empty;
                keySwapLines.Clear();
                continue;
            }

            if (keySwapBlockStarted)
                keySwapLines.Add(line);
        }

        _keySwaps = new List<KeySwapSection>(keySwaps.Count);

        foreach (var keySwap in keySwaps)
        {
            _keySwaps.Add(KeySwapSection.FromIniKeySwapSection(keySwap));
        }

        return new List<KeySwapSection>(_keySwaps).AsReadOnly();
    }


    public async Task SaveKeySwapConfiguration(ICollection<KeySwapSection> updatedKeySwaps,
        CancellationToken cancellationToken = default)
    {
        if (updatedKeySwaps.Count == 0)
            throw new ArgumentException("No key swaps to save.", nameof(updatedKeySwaps));

        if (updatedKeySwaps.Count != _keySwaps?.Count)
            throw new ArgumentException("Key swap count mismatch.", nameof(updatedKeySwaps));


        var fileLines = new List<string>();

        await using var fileStream =
            new FileStream(await GetIniPathAsync().ConfigureAwait(false), FileMode.Open, FileAccess.Read,
                FileShare.None);
        using (var reader = new StreamReader(fileStream))
        {
            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
                fileLines.Add(line);
        }

        var sectionStartIndexes = new List<int>();
        for (var i = 0; i < fileLines.Count; i++)
        {
            var currentLine = fileLines[i];
            if (updatedKeySwaps.Any(keySwap => IniConfigHelpers.IsSection(currentLine, keySwap.SectionName)))
                sectionStartIndexes.Add(i);
        }

        if (sectionStartIndexes.Count != updatedKeySwaps.Count)
            throw new InvalidOperationException("Key swap count mismatch.");

        if (sectionStartIndexes.Count == 0)
            throw new InvalidOperationException("No key swaps found.");

        // Line numbers where the key swap sections starts
        // We loop from the beginning to the end so we can remove or add lines without messing up the indexes
        for (var i = sectionStartIndexes.Count - 1; i >= 0; i--)
        {
            var keySwap = updatedKeySwaps.ElementAt(i);
            var sectionStartIndex = sectionStartIndexes[i] + 1;

            var newForwardKeyWrittenIndex = -1;
            var newBackwardKeyWrittenIndex = -1;

            var oldForwardKeyIndex = -1;
            var oldBackwardKeyIndex = -1;

            // When iterating a section go downwards instead of upwards
            // 8 as the limit is just an arbitrary number so it doesn't loop forever
            for (var lineIndex = sectionStartIndex; lineIndex < sectionStartIndex + 8; lineIndex++)
            {
                var line = fileLines[lineIndex];

                if (newForwardKeyWrittenIndex == -1 && IniConfigHelpers.IsIniKey(line, IniKeySwapSection.ForwardIniKey))
                {
                    var value = IniConfigHelpers.FormatIniKey(IniKeySwapSection.ForwardIniKey, keySwap.ForwardKey);
                    if (value is null)
                        continue;
                    fileLines[lineIndex] = value;

                    // If forwardkey is defined also set the backward key
                    if (keySwap.BackwardKey is null) continue;

                    var backwardValue =
                        IniConfigHelpers.FormatIniKey(IniKeySwapSection.BackwardIniKey, keySwap.BackwardKey);
                    if (backwardValue is null)
                        continue;
                    newBackwardKeyWrittenIndex = lineIndex + 1;
                    fileLines.Insert(newBackwardKeyWrittenIndex, backwardValue);
                }

                // Remove old forward key
                else if (newForwardKeyWrittenIndex != -1 && newForwardKeyWrittenIndex != lineIndex &&
                         IniConfigHelpers.IsIniKey(line, IniKeySwapSection.ForwardIniKey))
                {
                    oldForwardKeyIndex = lineIndex;
                }

                else if (newBackwardKeyWrittenIndex == -1 &&
                         IniConfigHelpers.IsIniKey(line, IniKeySwapSection.BackwardIniKey))
                {
                    var value = IniConfigHelpers.FormatIniKey(IniKeySwapSection.BackwardIniKey, keySwap.BackwardKey);
                    if (value is null)
                        continue;
                    fileLines[lineIndex] = value;

                    // If backwardkey is defined also set the forward key
                    if (keySwap.ForwardKey is null) continue;
                    var forwardValue =
                        IniConfigHelpers.FormatIniKey(IniKeySwapSection.ForwardIniKey, keySwap.ForwardKey);
                    if (forwardValue is null)
                        continue;
                    newForwardKeyWrittenIndex = lineIndex + 1;
                    fileLines.Insert(newForwardKeyWrittenIndex, forwardValue);
                }

                // Remove old backward key
                else if (newBackwardKeyWrittenIndex != -1 && newBackwardKeyWrittenIndex != lineIndex &&
                         IniConfigHelpers.IsIniKey(line, IniKeySwapSection.BackwardIniKey))
                {
                    oldBackwardKeyIndex = lineIndex;
                }

                else if (IniConfigHelpers.IsSection(line))
                {
                    break;
                }
            }

            if (newBackwardKeyWrittenIndex != -1 && newForwardKeyWrittenIndex != -1)
                throw new InvalidOperationException("Key bind writing error");

            if (oldBackwardKeyIndex != -1 && oldForwardKeyIndex != -1)
                throw new InvalidOperationException("key bind writing error");

            if (oldBackwardKeyIndex != -1)
                fileLines.RemoveAt(oldBackwardKeyIndex);
            else if (oldForwardKeyIndex != -1)
                fileLines.RemoveAt(oldForwardKeyIndex);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await using var writeStream =
            new FileStream(await GetIniPathAsync().ConfigureAwait(false), FileMode.Truncate, FileAccess.Write,
                FileShare.None);

        await using (var writer = new StreamWriter(writeStream))
        {
            foreach (var line in fileLines)
                await writer.WriteLineAsync(line).ConfigureAwait(false);
        }

        await ReadKeySwapConfiguration(CancellationToken.None).ConfigureAwait(false);
    }

    public OneOf<KeySwapSection[], KeySwapsNotLoaded> GetKeySwaps()
    {
        if (_keySwaps is null)
            return new KeySwapsNotLoaded();

        return _keySwaps.ToArray();
    }
}

public struct KeySwapsNotLoaded
{
}