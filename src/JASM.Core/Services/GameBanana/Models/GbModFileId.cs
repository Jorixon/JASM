﻿namespace GIMI_ModManager.Core.Services.GameBanana.Models;

/// <summary>
/// Example: https://gamebanana.com/dl/<see cref="GbModFileId"/>
/// </summary>
public record GbModFileId
{
    public string ModFileId { get; }

    public GbModFileId(string modFileId)
    {
        ModFileId = modFileId;
    }

    public GbModFileId(int modFileId)
    {
        ModFileId = modFileId.ToString();
    }

    public override string ToString() => ModFileId;

    public static implicit operator string(GbModFileId modFileId) => modFileId.ToString();
}