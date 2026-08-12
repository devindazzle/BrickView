using System.IO;
using System.Windows.Media.Imaging;

namespace BrickView;

public class ThumbnailLoader
{
    private const int WorkerCount = 4;

    private readonly object queueLock = new();

    private readonly PriorityQueue<
        ThumbnailLoadRequest,
        int> queue = new();

    private readonly SemaphoreSlim queueSignal =
        new SemaphoreSlim(0);

    private readonly List<Task> workers = new();

    private readonly IoFileReader reader =
        new IoFileReader();

    public ThumbnailLoader()
    {
        for (int i = 0; i < WorkerCount; i++)
        {
            workers.Add(
                Task.Run(WorkerAsync));
        }
    }

    public Task LoadAsync(
        IoFileListItem item,
        ThumbnailLoadPriority priority)
    {
        if (item.ThumbnailStatus !=
            ThumbnailStatus.NotLoaded)
        {
            return Task.CompletedTask;
        }

        item.ThumbnailStatus =
            ThumbnailStatus.Loading;

        ThumbnailLoadRequest request =
            new ThumbnailLoadRequest(item);

        lock (queueLock)
        {
            queue.Enqueue(
                request,
                (int)priority);
        }

        queueSignal.Release();

        return Task.CompletedTask;
    }

    private async Task WorkerAsync()
    {
        while (true)
        {
            await queueSignal.WaitAsync();

            ThumbnailLoadRequest? request = null;

            lock (queueLock)
            {
                if (queue.Count > 0)
                {
                    request = queue.Dequeue();
                }
            }

            if (request is null)
            {
                continue;
            }

            await LoadThumbnailAsync(
                request.Item);
        }
    }

    private async Task LoadThumbnailAsync(
        IoFileListItem item)
    {
        try
        {
            ThumbnailReadResult result =
                await Task.Run(
                    () => reader.ReadThumbnail(
                        item.FilePath));

            switch (result.Status)
            {
                case ThumbnailReadStatus.Loaded:

                    if (result.Data is null)
                    {
                        item.ErrorMessage =
                            "The thumbnail data was empty.";

                        item.ThumbnailStatus =
                            ThumbnailStatus.Error;

                        return;
                    }

                    BitmapImage thumbnail =
                        CreateBitmapImage(
                            result.Data);

                    item.Thumbnail =
                        thumbnail;

                    item.ThumbnailStatus =
                        ThumbnailStatus.Loaded;

                    break;

                case ThumbnailReadStatus.Missing:

                    item.ThumbnailStatus =
                        ThumbnailStatus.Missing;

                    break;

                case ThumbnailReadStatus.InvalidFile:

                    item.ErrorMessage =
                        result.ErrorMessage
                        ?? "The .io file is not a valid ZIP archive.";

                    item.ThumbnailStatus =
                        ThumbnailStatus.Error;

                    break;

                case ThumbnailReadStatus.Error:

                    item.ErrorMessage =
                        result.ErrorMessage
                        ?? "An unknown error occurred.";

                    item.ThumbnailStatus =
                        ThumbnailStatus.Error;

                    break;

                default:

                    item.ErrorMessage =
                        "Unknown thumbnail status.";

                    item.ThumbnailStatus =
                        ThumbnailStatus.Error;

                    break;
            }
        }
        catch (Exception exception)
        {
            item.ErrorMessage =
                exception.Message;

            item.ThumbnailStatus =
                ThumbnailStatus.Error;
        }
    }

    private static BitmapImage CreateBitmapImage(
        byte[] imageData)
    {
        using MemoryStream stream =
            new MemoryStream(imageData);

        BitmapImage image =
            new BitmapImage();

        image.BeginInit();

        image.CacheOption =
            BitmapCacheOption.OnLoad;

        image.StreamSource =
            stream;

        image.EndInit();

        image.Freeze();

        return image;
    }

    private sealed class ThumbnailLoadRequest
    {
        public IoFileListItem Item { get; }

        public ThumbnailLoadRequest(
            IoFileListItem item)
        {
            Item = item;
        }
    }
}