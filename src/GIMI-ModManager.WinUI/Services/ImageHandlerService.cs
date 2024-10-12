using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using GIMI_ModManager.Core.Helpers;
using Microsoft.UI.Xaml;

namespace GIMI_ModManager.WinUI.Services;

public class ImageHandlerService
{
    private readonly string _tmpFolder = Path.Combine(App.TMP_DIR, "Images");

    public readonly Uri PlaceholderImageUri = StaticPlaceholderImageUri;

    public static Uri StaticPlaceholderImageUri => new(Path.Combine(App.ASSET_DIR, "ModPanePlaceholder.webp"));

    private readonly IHttpClientFactory _httpClientFactory;

    public ImageHandlerService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public string PlaceholderImagePath => PlaceholderImageUri.LocalPath;

    public async Task<IStorageFile?> PickImageAsync(bool copyToTmpFolder = true, Window? window = null)
    {
        var filePicker = new FileOpenPicker();
        foreach (var supportedImageExtension in Constants.SupportedImageExtensions)
            filePicker.FileTypeFilter.Add(supportedImageExtension);

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window ?? App.MainWindow);
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


    public static Task CopyImageToClipboardAsync(StorageFile file)
    {
        ArgumentNullException.ThrowIfNull(file, nameof(file));

        var package = new DataPackage
        {
            RequestedOperation = DataPackageOperation.Copy
        };


        var imageStream = RandomAccessStreamReference.CreateFromFile(file);
        package.SetBitmap(imageStream);
        package.SetStorageItems([file]);

        Clipboard.SetContent(package);
        Clipboard.Flush();
        return Task.CompletedTask;
    }

    public async Task<bool> ClipboardContainsImageAsync()
    {
        var package = Clipboard.GetContent();

        if (package is null)
            return false;

        if (package.Contains(StandardDataFormats.Bitmap))
            return true;

        if (!package.Contains(StandardDataFormats.StorageItems))
            return false;

        var storageItems = await package.GetStorageItemsAsync();

        return storageItems.Any(item =>
            Constants.SupportedImageExtensions.Contains(Path.GetExtension(item.Name)));
    }

    public async Task<Uri?> GetImageFromClipboardAsync()
    {
        if (!await ClipboardContainsImageAsync().ConfigureAwait(false))
            return null;

        var package = Clipboard.GetContent();

        if (package is null)
            return null;

        if (package.Contains(StandardDataFormats.StorageItems))
        {
            var storageItems = await package.GetStorageItemsAsync();

            var imageFile = storageItems.FirstOrDefault(item =>
                Constants.SupportedImageExtensions.Contains(Path.GetExtension(item.Name)));

            if (imageFile is null || !File.Exists(imageFile.Path))
                return null;

            var copiedImage = await CopyImageToTmpFolder(await StorageFile.GetFileFromPathAsync(imageFile.Path))
                .ConfigureAwait(false);

            return new Uri(copiedImage.Path);
        }

        if (!package.Contains(StandardDataFormats.Bitmap))
            return null;

        var imageStream = await package.GetBitmapAsync();
        var availableFormats = package.AvailableFormats;

        var fileExtension = Constants.SupportedImageExtensions.FirstOrDefault(supportedFormat =>
            availableFormats.Any(availableFormat =>
                availableFormat.TrimStart('.')
                    .Equals(supportedFormat.TrimStart('.'), StringComparison.OrdinalIgnoreCase)));

        if (fileExtension is null)
            return null;

        using var stream = await imageStream.OpenReadAsync();

        var tmpFile = await CopyStreamToTmpFolder(stream.AsStreamForRead(), fileExtension);

        return new Uri(tmpFile.Path);
    }

    private async Task<StorageFile> CopyStreamToTmpFolder(Stream stream, string extensionWithDot)
    {
        var tmpFolder = new DirectoryInfo(_tmpFolder);

        if (!tmpFolder.Exists) tmpFolder.Create();

        var tmpFile = Path.Combine(tmpFolder.FullName,
            $"STREAM_DOWNLOAD_{Guid.NewGuid():N}{extensionWithDot}");

        await using var fileStream = File.Create(tmpFile);
        await stream.CopyToAsync(fileStream);

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

    public async Task<StorageFile?> CopyImageToTmpFolder(Uri? uri)
    {
        if (uri is null)
            return null;

        if (uri.Scheme == Uri.UriSchemeHttps && uri.IsAbsoluteUri)
        {
            return await DownloadImageAsync(uri).ConfigureAwait(false);
        }

        if (uri.Scheme == Uri.UriSchemeFile)
        {
            var file = await StorageFile.GetFileFromPathAsync(uri.LocalPath);
            return await CopyImageToTmpFolder(file).ConfigureAwait(false);
        }

        return null;
    }
}