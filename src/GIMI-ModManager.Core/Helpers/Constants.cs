﻿namespace GIMI_ModManager.Core.Helpers;

public static class Constants
{
    public static readonly IReadOnlyCollection<string> SupportedImageExtensions = new[]
        { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".ico", ".svg", ".webp", ".bitmap" };

    public static readonly string ModConfigFileName = ".JASM_ModConfig.json";
    public static readonly string ShaderFixesFolderName = "ShaderFixes";
    public static readonly string MergedIniName = "merged.ini";
}