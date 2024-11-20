namespace GIMI_ModManager.Core.Services.CommandService.Models;

public sealed class SpecialVariablesInput
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