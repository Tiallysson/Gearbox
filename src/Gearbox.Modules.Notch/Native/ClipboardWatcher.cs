using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Gearbox.Interop.Clipboard;
using Gearbox.Modules.Notch.Logic;
using Microsoft.Extensions.Logging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

namespace Gearbox.Modules.Notch.Native;

internal sealed class ClipboardWatcher : IDisposable
{
    private const ulong MaxImageBytes = 20 * 1024 * 1024;
    private const uint ThumbnailSize = 192;

    private readonly ClipboardHistory _history = new();
    private readonly Dictionary<long, InMemoryRandomAccessStream> _images = [];
    private readonly ClipboardMonitor _monitor;
    private readonly ILogger _logger;
    private long _nextId;
    private int _readVersion;
    private bool _disposed;

    public ClipboardWatcher(ILogger logger)
    {
        _logger = logger;
        _monitor = new ClipboardMonitor(logger);
        _monitor.Changed += OnClipboardChanged;
    }

    public event EventHandler? Changed;

    public IReadOnlyList<ClipboardItem> Items => _history.Items;

    public IRandomAccessStream? OpenImage(long id) => _images.TryGetValue(id, out var image) ? image.CloneStream() : null;

    public async Task<bool> CopyBackAsync(long id)
    {
        var item = _history.Find(id);
        if (item is null)
        {
            return false;
        }

        var image = item.Kind == ClipboardItemKind.Image ? OpenImage(id) : null;
        try
        {
            var package = await CreatePackageAsync(item, image);
            if (package is null || _disposed)
            {
                return false;
            }

            Clipboard.SetContent(package);
            Clipboard.Flush();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Não foi possível copiar o item {Id} de volta para a área de transferência", id);
            return false;
        }
        finally
        {
            image?.Dispose();
        }
    }

    public void Remove(long id)
    {
        if (_history.Remove(id) is { } removed)
        {
            Release(removed);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Clear()
    {
        var removed = _history.Clear();
        foreach (var item in removed)
        {
            Release(item);
        }

        if (removed.Count > 0)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _monitor.Changed -= OnClipboardChanged;
        try
        {
            _monitor.Dispose();
        }
        finally
        {
            foreach (var image in _images.Values)
            {
                image.Dispose();
            }

            _images.Clear();
            _history.Clear();
        }
    }

    private async void OnClipboardChanged(object? sender, ClipboardChangedEventArgs e)
    {
        var version = ++_readVersion;
        if (e.IsExcluded)
        {
            return;
        }

        for (var failedAttempts = 0; ; failedAttempts++)
        {
            try
            {
                var (item, image) = await ReadAsync();
                if (IsStale(version, e.SequenceNumber) || item is null)
                {
                    image?.Dispose();
                    return;
                }

                Add(item, image);
                return;
            }
            catch (COMException ex) when (ClipboardReadErrors.IsTransient(ex.HResult) && ClipboardReadErrors.RetryDelay(failedAttempts + 1) is { } delay)
            {
                _logger.LogDebug(ex, "Área de transferência ocupada; nova tentativa {Attempt} em {Delay} ms", failedAttempts + 1, delay.TotalMilliseconds);
                await Task.Delay(delay);
                if (IsStale(version, e.SequenceNumber))
                {
                    return;
                }
            }
            catch (COMException ex) when (ClipboardReadErrors.IsTransient(ex.HResult))
            {
                _logger.LogWarning(ex, "A área de transferência continuou ocupada depois de {Attempts} tentativas; o item copiado ficou fora do histórico", failedAttempts + 1);
                return;
            }
            catch (Exception ex)
            {
                LogReadFailure(ex, "Não foi possível ler a área de transferência");
                return;
            }
        }
    }

    private bool IsStale(int version, uint sequenceNumber) =>
        _disposed || version != _readVersion || ClipboardMonitor.SequenceNumber != sequenceNumber;

    private void LogReadFailure(Exception ex, string message)
    {
        if (ex is COMException && ClipboardReadErrors.IsTransient(ex.HResult))
        {
            _logger.LogDebug(ex, message);
        }
        else
        {
            _logger.LogWarning(ex, message);
        }
    }

    private async Task<(ClipboardItem? Item, InMemoryRandomAccessStream? Image)> ReadAsync()
    {
        var content = Clipboard.GetContent();
        if (content.Contains(StandardDataFormats.StorageItems))
        {
            return await ReadFilesAsync(content);
        }

        if (content.Contains(StandardDataFormats.Text))
        {
            var text = await content.GetTextAsync();
            return ClipboardPreview.ForText(text) is null
                ? (null, null)
                : (new ClipboardItem(++_nextId, ClipboardItemKind.Text, text, null, DateTimeOffset.Now), null);
        }

        if (content.Contains(StandardDataFormats.Bitmap))
        {
            return await ReadBitmapAsync(content);
        }

        _logger.LogDebug("Área de transferência sem formato suportado: {Formats}", string.Join(", ", content.AvailableFormats));
        return (null, null);
    }

    private async Task<(ClipboardItem? Item, InMemoryRandomAccessStream? Image)> ReadFilesAsync(DataPackageView content)
    {
        var storageItems = await content.GetStorageItemsAsync();
        var paths = storageItems.Select(storageItem => storageItem.Path).Where(path => !string.IsNullOrEmpty(path)).ToList();
        if (paths.Count == 0)
        {
            return (null, null);
        }

        var thumbnail = await ReadThumbnailAsync(paths[0]);
        var item = new ClipboardItem(++_nextId, ClipboardItemKind.Files, string.Join('\n', paths), null, DateTimeOffset.Now);
        return (item, thumbnail);
    }

    private async Task<InMemoryRandomAccessStream?> ReadThumbnailAsync(string path)
    {
        InMemoryRandomAccessStream? copy = null;
        try
        {
            IStorageItemProperties? properties = File.Exists(path)
                ? await StorageFile.GetFileFromPathAsync(path)
                : Directory.Exists(path) ? await StorageFolder.GetFolderFromPathAsync(path) : null;
            if (properties is null)
            {
                return null;
            }

            using var thumbnail = await properties.GetThumbnailAsync(ThumbnailMode.SingleItem, ThumbnailSize, ThumbnailOptions.ResizeThumbnail);
            if (thumbnail is null || thumbnail.Size == 0)
            {
                _logger.LogDebug("Arquivo copiado sem miniatura");
                return null;
            }

            copy = new InMemoryRandomAccessStream();
            await RandomAccessStream.CopyAsync(thumbnail, copy);
            _logger.LogDebug("Miniatura do arquivo copiado: {Type}, {Bytes} bytes", thumbnail.Type, copy.Size);
            return copy;
        }
        catch (Exception ex)
        {
            copy?.Dispose();
            LogReadFailure(ex, "Não foi possível ler a miniatura do arquivo copiado");
            return null;
        }
    }

    private async Task<(ClipboardItem? Item, InMemoryRandomAccessStream? Image)> ReadBitmapAsync(DataPackageView content)
    {
        var reference = await content.GetBitmapAsync();
        using var source = await reference.OpenReadAsync();
        if (source.Size > MaxImageBytes)
        {
            return (null, null);
        }

        var image = new InMemoryRandomAccessStream();
        try
        {
            await RandomAccessStream.CopyAsync(source, image);
            await using var hashSource = image.CloneStream().AsStreamForRead();
            var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(hashSource));
            return (new ClipboardItem(++_nextId, ClipboardItemKind.Image, null, hash, DateTimeOffset.Now), image);
        }
        catch
        {
            image.Dispose();
            throw;
        }
    }

    private static async Task<DataPackage?> CreatePackageAsync(ClipboardItem item, IRandomAccessStream? image)
    {
        var package = new DataPackage();
        switch (item.Kind)
        {
            case ClipboardItemKind.Text when item.Text is not null:
                package.SetText(item.Text);
                return package;

            case ClipboardItemKind.Image when image is not null:
                package.SetBitmap(RandomAccessStreamReference.CreateFromStream(image));
                return package;

            case ClipboardItemKind.Files:
                var storageItems = await OpenStorageItemsAsync(ClipboardPreview.FilePaths(item.Text));
                if (storageItems.Count == 0)
                {
                    return null;
                }

                package.SetStorageItems(storageItems);
                return package;

            default:
                return null;
        }
    }

    private static async Task<List<IStorageItem>> OpenStorageItemsAsync(IReadOnlyList<string> paths)
    {
        var storageItems = new List<IStorageItem>();
        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                storageItems.Add(await StorageFile.GetFileFromPathAsync(path));
            }
            else if (Directory.Exists(path))
            {
                storageItems.Add(await StorageFolder.GetFolderFromPathAsync(path));
            }
        }

        return storageItems;
    }

    private void Add(ClipboardItem item, InMemoryRandomAccessStream? image)
    {
        if (image is not null)
        {
            _images[item.Id] = image;
        }

        foreach (var removed in _history.Add(item))
        {
            Release(removed);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Release(ClipboardItem item)
    {
        if (_images.Remove(item.Id, out var image))
        {
            image.Dispose();
        }
    }
}
