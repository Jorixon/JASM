using Windows.Storage;
using Windows.Storage.Pickers;
using GIMI_ModManager.Core.Helpers;

namespace GIMI_ModManager.WinUI.Services;

public class ImageHandlerService
{
    private readonly DirectoryInfo _tmpFolder = new(Path.Combine(App.TMP_DIR, "Images"));

    public readonly Uri PlaceholderImageUri =
        new(Path.Combine(App.ASSET_DIR, "ModPanePlaceholder.webp"));

    private readonly IHttpClientFactory _httpClientFactory;

    public ImageHandlerService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public string PlaceholderImagePath => PlaceholderImageUri.LocalPath;

    public async Task<IStorageFile?> PickImageAsync(bool copyToTmpFolder = true)
    {
        var filePicker = new FileOpenPicker();
        foreach (var supportedImageExtension in Constants.SupportedImageExtensions)
            filePicker.FileTypeFilter.Add(supportedImageExtension);

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

        var file = await filePicker.PickSingleFileAsync();

        if (file == null) return null;

        if (copyToTmpFolder)
            file = await CopyImageToTmpFolder(file);

        return file;
    }


    public async Task<StorageFile> DownloadImageAsync(Uri url, CancellationToken cancellationToken = default)
    {
        if (url.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("Url must be https", nameof(url));

        if (!url.IsAbsoluteUri)
            throw new ArgumentException("Url must be absolute", nameof(url));


        if (!Constants.SupportedImageExtensions.Contains(Path.GetExtension(url.AbsolutePath)))
        {
            var invalidExtension = Path.GetExtension(url.AbsolutePath);

            invalidExtension = string.IsNullOrWhiteSpace(invalidExtension)
                ? "Could determine extension"
                : invalidExtension;

            throw new ArgumentException($"Url must be a valid image url. Invalid extension: {invalidExtension}");
        }


        var tmpFile = Path.Combine(_tmpFolder.FullName,
            $"WEB_DOWNLOAD_{Guid.NewGuid():N}{Path.GetExtension(url.ToString())}");


        if (!_tmpFolder.Exists)
            _tmpFolder.Create();

        var client = _httpClientFactory.CreateClient();

        var responseStream = await client.GetStreamAsync(url, cancellationToken).ConfigureAwait(false);

        await using var fileStream = File.Create(tmpFile);
        await responseStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);

        return await StorageFile.GetFileFromPathAsync(tmpFile);
    }


    private async Task<StorageFile> CopyImageToTmpFolder(StorageFile file)
    {
        if (!_tmpFolder.Exists) _tmpFolder.Create();

        var tmpFile = new FileInfo(Path.Combine(_tmpFolder.FullName, file.Name));
        if (tmpFile.Exists) tmpFile.Delete();

        var tmpImage = await file.CopyAsync(await StorageFolder.GetFolderFromPathAsync(_tmpFolder.FullName));
        var extension = tmpImage.FileType;

        var newFileName = $"{Path.GetFileNameWithoutExtension(tmpImage.Name)}_{Guid.NewGuid()}{extension}";

        await tmpImage.RenameAsync(newFileName);

        return tmpImage;
    }
}