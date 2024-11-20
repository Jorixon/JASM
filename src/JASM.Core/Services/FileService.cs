using System.Text;
using GIMI_ModManager.Core.Contracts.Services;
using Newtonsoft.Json;

namespace GIMI_ModManager.Core.Services;

public class FileService : IFileService
{
    private readonly object _fileLock = new();

    public T Read<T>(string folderPath, string fileName) where T : new()
    {
        var path = Path.Combine(folderPath, fileName);
        if (!File.Exists(path)) return new T();

        lock (_fileLock)
        {
            var json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<T>(json) ?? new T();
        }
    }

    public void Save<T>(string folderPath, string fileName, T content, bool serializeContent = true)
    {
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }


        var fileContent = serializeContent
            ? JsonConvert.SerializeObject(content, Formatting.Indented)
            : content?.ToString();

        lock (_fileLock)
        {
            File.WriteAllText(Path.Combine(folderPath, fileName), fileContent, Encoding.UTF8);
        }
    }

    public void Delete(string folderPath, string fileName)
    {
        if (!File.Exists(Path.Combine(folderPath, fileName))) return;

        lock (_fileLock)
        {
            File.Delete(Path.Combine(folderPath, fileName));
        }
    }
}