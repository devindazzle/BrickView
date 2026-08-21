// -----------------------------------------------------------------------------
// ThumbnailLoader.cs
//
// Provides asynchronous thumbnail loading for BrickView model items.
//
// Responsibilities:
// - Maintains a prioritized queue of thumbnail load requests.
// - Prevents duplicate requests for the same model item.
// - Allows higher-priority requests to supersede lower-priority queued requests.
// - Processes thumbnail requests using a fixed number of background workers.
// - Reads thumbnail data from .io files through IoFileReader.
// - Converts thumbnail data into WPF BitmapImage instances.
// - Updates the corresponding IoFileListItem with the thumbnail status,
//   image and any loading error.
//
// Thumbnail requests are prioritized so visible thumbnails can be processed
// before thumbnails that were requested only for preloading.
//
// The loader owns the worker queue but does not decide which models should be
// visible. Viewport and application-level code determine when requests are
// submitted.
//
// ThumbnailSizeManager provides the current thumbnail dimensions when the
// image is decoded.
// -----------------------------------------------------------------------------

using System.IO;
using System.Windows.Media.Imaging;

namespace BrickView;

/// <summary>
/// Loads BrickView model thumbnails asynchronously using a prioritized
/// background worker queue.
/// </summary>
public class ThumbnailLoader {
    /// <summary>
    /// Gets the number of background workers used to process thumbnail requests.
    /// </summary>
    private const int WorkerCount = 4;

    private readonly ThumbnailSizeManager thumbnailSizeManager;

    private readonly object queueLock = new();

    private readonly PriorityQueue<
        ThumbnailLoadRequest,
        int> queue =
        new();

    private readonly Dictionary<
        IoFileListItem,
        ThumbnailLoadPriority> queuedPriorities =
        new();

    private readonly SemaphoreSlim queueSignal =
        new SemaphoreSlim(0);

    private readonly List<Task> workers =
        new();

    private readonly IoFileReader reader =
        new IoFileReader();

    private int sequenceNumber;

    /// <summary>
    /// Initializes the thumbnail loader and starts its background workers.
    /// </summary>
    /// <remarks>
    /// The current thumbnail dimensions used for decoding are obtained from
    /// <see cref="ThumbnailSizeManager"/> when each image is created.
    /// </remarks>
    public ThumbnailLoader() {
        thumbnailSizeManager =
            ThumbnailSizeManager.Instance;

        // Start the fixed worker pool once. Workers wait on queueSignal until
        // a thumbnail request is submitted.
        for (int i = 0;
             i < WorkerCount;
             i++) {

            workers.Add(
                Task.Run(
                    WorkerAsync));
        }
    }

    /// <summary>
    /// Queues a thumbnail request for the specified model item.
    /// </summary>
    /// <param name="item">
    /// The model item whose thumbnail should be loaded.
    /// </param>
    /// <param name="priority">
    /// The priority assigned to the thumbnail request.
    /// </param>
    /// <returns>
    /// A completed task after the request has been accepted by the queue.
    /// </returns>
    /// <remarks>
    /// A model that already has a terminal thumbnail state is not queued.
    /// When the same model is already queued, a request is only replaced when
    /// the new request has a higher priority.
    /// </remarks>
    public Task LoadAsync(
        IoFileListItem item,
        ThumbnailLoadPriority priority) {
        ArgumentNullException.ThrowIfNull(
            item);

        lock (queueLock) {
            if (item.ThumbnailStatus ==
                    ThumbnailStatus.Loaded ||
                item.ThumbnailStatus ==
                    ThumbnailStatus.Missing ||
                item.ThumbnailStatus ==
                    ThumbnailStatus.Error) {

                return Task.CompletedTask;
            }

            if (queuedPriorities.TryGetValue(
                    item,
                    out ThumbnailLoadPriority existingPriority)) {

                if (priority >= existingPriority) {
                    return Task.CompletedTask;
                }

                queuedPriorities[item] =
                    priority;
            }
            else {
                queuedPriorities.Add(
                    item,
                    priority);

                item.ThumbnailStatus =
                    ThumbnailStatus.Loading;
            }

            // The priority is the primary queue key. sequenceNumber ensures
            // requests with the same priority retain their submission order.
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

    /// <summary>
    /// Waits for queued thumbnail requests and processes them on a background
    /// worker until the loader is terminated with the application.
    /// </summary>
    /// <returns>
    /// A task representing the lifetime of the worker.
    /// </returns>
    private async Task WorkerAsync() {
        while (true) {
            await queueSignal.WaitAsync();

            ThumbnailLoadRequest? request;

            lock (queueLock) {
                if (queue.Count == 0) {
                    continue;
                }

                request =
                    queue.Dequeue();

                if (!queuedPriorities.TryGetValue(
                        request.Item,
                        out ThumbnailLoadPriority currentPriority)) {
                    continue;
                }

                // A newer request for the same item may have superseded this
                // queue entry. Such stale entries are intentionally discarded.
                if (currentPriority !=
                    request.Priority) {
                    continue;
                }

                queuedPriorities.Remove(
                    request.Item);
            }

            await LoadThumbnailAsync(
                request.Item);
        }
    }

    /// <summary>
    /// Reads the thumbnail from the model file and updates the model item with
    /// the resulting image or an appropriate error state.
    /// </summary>
    /// <param name="item">
    /// The model item whose thumbnail should be loaded.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous thumbnail loading operation.
    /// </returns>
    private async Task LoadThumbnailAsync(
        IoFileListItem item) {
        try {
            ThumbnailReadResult result =
                await Task.Run(
                    () =>
                        reader.ReadThumbnail(
                            item.FilePath));

            switch (result.Status) {
                case ThumbnailReadStatus.Loaded:

                    if (result.Data is null) {
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
        catch (Exception exception) {
            item.ErrorMessage =
                exception.Message;

            item.ThumbnailStatus =
                ThumbnailStatus.Error;
        }
    }

    /// <summary>
    /// Creates a WPF bitmap from raw thumbnail image data using the current
    /// thumbnail width configured by <see cref="ThumbnailSizeManager"/>.
    /// </summary>
    /// <param name="imageData">
    /// The encoded thumbnail image data.
    /// </param>
    /// <returns>
    /// A frozen <see cref="BitmapImage"/> suitable for use by the WPF UI.
    /// </returns>
    private BitmapImage CreateBitmapImage(
        byte[] imageData) {
        ArgumentNullException.ThrowIfNull(
            imageData);

        using MemoryStream stream =
            new MemoryStream(
                imageData);

        BitmapImage image =
            new BitmapImage();

        image.BeginInit();

        // OnLoad ensures the bitmap no longer depends on the source stream
        // after initialization, allowing the stream to be disposed safely.
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

        // Freezing makes the bitmap immutable and allows it to be safely
        // consumed by the WPF UI across the worker/UI thread boundary.
        image.Freeze();

        return image;
    }

    /// <summary>
    /// Represents one thumbnail request stored in the priority queue.
    /// </summary>
    private sealed class ThumbnailLoadRequest {
        /// <summary>
        /// Gets the model item whose thumbnail should be loaded.
        /// </summary>
        public IoFileListItem Item {
            get;
        }

        /// <summary>
        /// Gets the priority assigned to this thumbnail request.
        /// </summary>
        public ThumbnailLoadPriority Priority {
            get;
        }

        /// <summary>
        /// Initializes a new thumbnail load request.
        /// </summary>
        /// <param name="item">
        /// The model item whose thumbnail should be loaded.
        /// </param>
        /// <param name="priority">
        /// The priority assigned to the request.
        /// </param>
        public ThumbnailLoadRequest(
            IoFileListItem item,
            ThumbnailLoadPriority priority) {
            Item =
                item;

            Priority =
                priority;
        }
    }
}