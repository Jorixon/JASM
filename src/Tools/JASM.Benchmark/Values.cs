namespace JASM.Benchmark;

public static class Values
{
    public static readonly string ActiveModsFolderPath = Path.Combine(Helpers.GetDevProjectSrcFolder().Parent!.FullName, "Testing\\Mods");
    public static readonly string ThreeMigotoRootfolder = Path.Combine(Helpers.GetDevProjectSrcFolder().Parent!.FullName, "Testing");
    public static readonly string TestModFolderPath = Path.Combine(Helpers.GetDevProjectSrcFolder()!.FullName, "Tools\\JASM.Benchmark\\Testing\\TestMod");
}