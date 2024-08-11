using System.Diagnostics.CodeAnalysis;

namespace GIMI_ModManager.Core.Services.CommandService;

public static class SpecialVariables
{
    public static IReadOnlyList<string> AllVariables => [TargetPath];


    public const string TargetPath = "{{TargetPath}}";


    [return: NotNullIfNotNull(nameof(input))]
    public static string? ReplaceVariables(string? input, SpecialVariablesInput? specialVariables)
    {
        if (input is null)
            return null;

        if (specialVariables is null || !specialVariables.HasAnySpecialVariables())
            return input;

        foreach (var variable in specialVariables.GetSpecialVariables())
        {
            input = input.Replace(variable, specialVariables.GetVariable(variable));
        }

        return input;
    }
}

public class SpecialVariablesInput
{
    private readonly Dictionary<string, string?> _variables = [];

    public bool IsReadOnly { get; internal set; }

    public bool HasAnySpecialVariables() =>
        _variables.Any(kv => SpecialVariables.AllVariables.Contains(kv.Key));

    public bool HasSpecialVariable(string variable) =>
        SpecialVariables.AllVariables.Contains(variable) && _variables.ContainsKey(variable);


    private void EnsureNotReadOnly()
    {
        if (IsReadOnly)
            throw new InvalidOperationException("The object is read-only");
    }

    public void SetVariable(string variable, string value)
    {
        if (!SpecialVariables.AllVariables.Contains(variable))
            throw new ArgumentException($"The variable '{variable}' is not a special variable");

        EnsureNotReadOnly();

        _variables[variable] = value;
    }

    public static SpecialVariablesInput CreateWithTargetPath(string targetPath)
    {
        var input = new SpecialVariablesInput();
        input.SetVariable(SpecialVariables.TargetPath, targetPath);
        return input;
    }

    public IEnumerable<string> GetSpecialVariables()
    {
        return _variables.Keys;
    }

    public string GetVariable(string variable)
    {
        if (!SpecialVariables.AllVariables.Contains(variable))
            throw new ArgumentException($"The variable '{variable}' is not a special variable");

        return _variables[variable] ?? throw new ArgumentException($"The variable '{variable}' is not set");
    }
}