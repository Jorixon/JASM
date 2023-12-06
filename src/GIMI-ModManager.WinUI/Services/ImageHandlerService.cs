using Windows.Storage;
using Windows.Storage.Pickers;
using GIMI_ModManager.Core.Helpers;

namespace GIMI_ModManager.WinUI.Services;

public class ImageHandlerService
{
    private readonly string _tmpFolder = Path.Combine(App.TMP_DIR, "Images");

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

        var tmpFolder = new DirectoryInfo(_tmpFolder);

        var tmpFile = Path.Combine(tmpFolder.FullName,
            $"WEB_DOWNLOAD_{Guid.NewGuid():N}{Path.GetExtension(url.ToString())}");


        if (!tmpFolder.Exists)
            tmpFolder.Create();

        var client = _httpClientFactory.CreateClient();

        var responseStream = await client.GetStreamAsync(url, cancellationToken).ConfigureAwait(false);

        await using var fileStream = File.Create(tmpFile);
        await responseStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);

        return await StorageFile.GetFileFromPathAsync(tmpFile);
    }


    private async Task<StorageFile> CopyImageToTmpFolder(StorageFile file)
    {
        var tmpFolder = new DirectoryInfo(_tmpFolder);

        if (!tmpFolder.Exists) tmpFolder.Create();

        var tmpFile = new FileInfo(Path.Combine(tmpFolder.FullName, file.Name));
        if (tmpFile.Exists) tmpFile.Delete();

        var tmpImage = await file.CopyAsync(await StorageFolder.GetFolderFromPathAsync(tmpFolder.FullName));
        var extension = tmpImage.FileType;

        var newFileName = $"{Path.GetFileNameWithoutExtension(tmpImage.Name)}_{Guid.NewGuid()}{extension}";

        await tmpImage.RenameAsync(newFileName);

        return tmpImage;
    }
}