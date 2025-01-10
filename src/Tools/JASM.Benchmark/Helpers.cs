namespace JASM.Benchmark;

public static class Helpers
{
    private const string SolutionFile = "GIMI-ModManager.sln";


    public static DirectoryInfo GetDevProjectSrcFolder()
    {
        DirectoryInfo currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());

        DirectoryInfo? devProjectFolder = null;

        while (true)
        {
            if (currentDir.GetFiles(SolutionFile).Any())
            {
                devProjectFolder = currentDir;
                break;
            }

            currentDir = currentDir.Parent ?? throw new DirectoryNotFoundException("Could not find the project folder.");
        }

        return devProjectFolder;
    }

    public static DirectoryInfo GetGamesFolder(string game)
    {
        var devProjectFolder = GetDevProjectSrcFolder();

        var assetsFolder = new DirectoryInfo(Path.Combine(devProjectFolder.FullName, $"GIMI-ModManager.WinUI\\Assets\\Games\\{game}"));

        if (!assetsFolder.Exists)
            throw new DirectoryNotFoundException($"Could not find the game folder for {game}");

        return assetsFolder;
    }

    public static DirectoryInfo GetTmpFolder()
    {
        var tmpFolder = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "JASM_TMP", Guid.NewGuid().ToString()));
        if (!tmpFolder.Exists)
            tmpFolder.Create();
        return tmpFolder;
    }
}