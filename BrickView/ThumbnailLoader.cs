using System.IO;
using System.Windows.Media.Imaging;

namespace BrickView;

public class ThumbnailLoader
{
    private const int WorkerCount = 4;

    private readonly ThumbnailSizeManager thumbnailSizeManager;

    private readonly object queueLock = new();

    private readonly PriorityQueue<
        ThumbnailLoadRequest,
        int> queue = new();

    private readonly Dictionary<
        IoFileListItem,
        ThumbnailLoadPriority> queuedPriorities =
        new();

    private readonly SemaphoreSlim queueSignal =
        new SemaphoreSlim(0);

    private readonly List<Task> workers = new();

    private readonly IoFileReader reader =
        new IoFileReader();

    private int sequenceNumber;

    public ThumbnailLoader(
        double thumbnailWidth)
    {
        if (thumbnailWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(thumbnailWidth));
        }

        thumbnailSizeManager =
            ThumbnailSizeManager.Instance;

        for (int i = 0;
             i < WorkerCount;
             i++)
        {
            workers.Add(
                Task.Run(WorkerAsync));
        }
    }

    public Task LoadAsync(
        IoFileListItem item,
        ThumbnailLoadPriority priority)
    {
        lock (queueLock)
        {
            if (item.ThumbnailStatus ==
                    ThumbnailStatus.Loaded ||
                item.ThumbnailStatus ==
                    ThumbnailStatus.Missing ||
                item.ThumbnailStatus ==
                    ThumbnailStatus.Error)
            {
                return Task.CompletedTask;
            }

            if (queuedPriorities.TryGetValue(
                    item,
                    out ThumbnailLoadPriority existingPriority))
            {
                if (priority >= existingPriority)
                {
                    return Task.CompletedTask;
                }

                queuedPriorities[item] =
                    priority;
            }
            else
            {
                queuedPriorities.Add(
                    item,
                    priority);

                item.ThumbnailStatus =
                    ThumbnailStatus.Loading;
            }

            int queuePriority =
                ((int)priority * 1_000_000)
                + sequenceNumber;

            sequenceNumber++;

            ThumbnailLoadRequest request =
                new ThumbnailLoadRequest(
                    item,
                    priority);

            queue.Enqueue(
                request,
                queuePriority);
        }

        queueSignal.Release();

        return Task.CompletedTask;
    }

    private async Task WorkerAsync()
    {
        while (true)
        {
            await queueSignal.WaitAsync();

            ThumbnailLoadRequest? request;

            lock (queueLock)
            {
                if (queue.Count == 0)
                {
                    continue;
                }

                request = queue.Dequeue();

                if (!queuedPriorities.TryGetValue(
                        request.Item,
                        out ThumbnailLoadPriority currentPriority))
                {
                    continue;
                }

                if (currentPriority !=
                    request.Priority)
                {
                    continue;
                }

                queuedPriorities.Remove(
                    request.Item);
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

    private BitmapImage CreateBitmapImage(
        byte[] imageData)
    {
        using MemoryStream stream =
            new MemoryStream(imageData);

        BitmapImage image =
            new BitmapImage();

        image.BeginInit();

        image.CacheOption =
            BitmapCacheOption.OnLoad;

        double thumbnailWidth =
            thumbnailSizeManager.Current.ThumbnailWidth;

        image.DecodePixelWidth =
            (int)Math.Round(
                thumbnailWidth);

        image.StreamSource =
            stream;

        image.EndInit();

        image.Freeze();

        return image;
    }

    private sealed class ThumbnailLoadRequest
    {
        public IoFileListItem Item { get; }

        public ThumbnailLoadPriority Priority { get; }

        public ThumbnailLoadRequest(
            IoFileListItem item,
            ThumbnailLoadPriority priority)
        {
            Item = item;
            Priority = priority;
        }
    }
}