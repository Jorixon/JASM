namespace GIMI_ModManager.Core.Contracts.Services;

public interface IFileService
{
    T Read<T>(string folderPath, string fileName);

    void Save<T>(string folderPath, string fileName, T content, bool serializeContent = true);

    void Delete(string folderPath, string fileName);
}
