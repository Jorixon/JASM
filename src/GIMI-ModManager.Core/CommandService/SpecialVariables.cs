using System.Diagnostics.CodeAnalysis;

namespace GIMI_ModManager.Core.CommandService;

public static class SpecialVariables
{
    public static IReadOnlyList<string> AllVariables => [TargetPath];


    public const string TargetPath = "{{TargetPath}}";


    [return: NotNullIfNotNull(nameof(input))]
    public static string? ReplaceVariables(string? input, string targetPath)
    {
        return input?.Replace(TargetPath, targetPath);
    }
}