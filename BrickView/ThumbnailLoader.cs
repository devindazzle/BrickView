// -----------------------------------------------------------------------------
// ThumbnailLoader.cs
//
// Loads .io model thumbnails asynchronously using a fixed worker pool and a
// priority queue.
//
// Requests are prioritized so visible models are processed before preloaded
// models. A queued request can be superseded by a higher-priority request for
// the same model.
//
// Active requests are tracked separately from queued requests so repeated
// viewport notifications cannot start duplicate thumbnail reads for a model
// that is already being processed.
//
// Each request captures the IoFileListItem thumbnail generation at the time the
// request is accepted. If the thumbnail is invalidated while the request is
// loading, IoFileListItem rejects the stale result instead of allowing an older
// thumbnail to overwrite the newer state.
// -----------------------------------------------------------------------------

using System.IO;
using System.Windows.Media.Imaging;

namespace BrickView;

/// <summary>
/// Loads thumbnails asynchronously from BrickLink Studio .io files.
/// </summary>
public sealed class ThumbnailLoader {
    private const int WorkerCount = 4;

    private readonly ThumbnailSizeManager thumbnailSizeManager;

    private readonly PriorityQueue<
        ThumbnailLoadRequest,
        int> queue =
        new PriorityQueue<
            ThumbnailLoadRequest,
            int>();

    private readonly object queueLock =
        new object();

    private readonly Dictionary<
        IoFileListItem,
        ThumbnailLoadPriority> queuedPriorities =
        new Dictionary<
            IoFileListItem,
            ThumbnailLoadPriority>();

    private readonly Dictionary<
        IoFileListItem,
        ThumbnailLoadPriority> activePriorities =
        new Dictionary<
            IoFileListItem,
            ThumbnailLoadPriority>();

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
    ///
    /// When the model is already being processed by a worker, another request
    /// does not start a second thumbnail read. Instead, a higher-priority
    /// request is remembered so it can be scheduled again if the active load
    /// becomes stale.
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

            // Do not start a second thumbnail read while this item is already
            // being processed. Remember a higher-priority request so it can
            // be scheduled after the active request has completed if needed.
            if (activePriorities.TryGetValue(
                    item,
                    out ThumbnailLoadPriority activePriority)) {

                if (priority <
                    activePriority) {

                    activePriorities[item] =
                        priority;
                }

                return Task.CompletedTask;
            }

            if (queuedPriorities.TryGetValue(
                    item,
                    out ThumbnailLoadPriority existingPriority)) {

                if (priority >=
                    existingPriority) {

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

            // Capture the generation after the request has been accepted.
            // Invalidation creates a new generation, allowing active loads
            // from this request to be recognized as stale when they finish.
            int thumbnailGeneration =
                item.ThumbnailGeneration;

            // The priority is the primary queue key. sequenceNumber ensures
            // requests with the same priority retain their submission order.
            int queuePriority =
                ((int)priority * 1_000_000)
                + sequenceNumber;

            sequenceNumber++;

            ThumbnailLoadRequest request =
                new ThumbnailLoadRequest(
                    item,
                    priority,
                    thumbnailGeneration);

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

                // From this point until LoadThumbnailAsync completes, this
                // model is considered active. Further viewport requests must
                // not start another concurrent thumbnail read.
                activePriorities[request.Item] =
                    request.Priority;
            }

            try {
                await LoadThumbnailAsync(
                    request);
            }
            finally {
                lock (queueLock) {
                    activePriorities.Remove(
                        request.Item);
                }
            }

            // If a new request arrived while the previous request was active,
            // LoadAsync has already recorded its desired priority. If the
            // thumbnail generation changed while the active request was
            // running, schedule a new request using that remembered priority.
            lock (queueLock) {
                if (request.Item.ThumbnailGeneration !=
                    request.ThumbnailGeneration &&
                    !queuedPriorities.ContainsKey(
                        request.Item) &&
                    !activePriorities.ContainsKey(
                        request.Item) &&
                    request.Item.ThumbnailStatus !=
                        ThumbnailStatus.Loaded &&
                    request.Item.ThumbnailStatus !=
                        ThumbnailStatus.Missing &&
                    request.Item.ThumbnailStatus !=
                        ThumbnailStatus.Error) {

                    ThumbnailLoadPriority retryPriority =
                        request.Priority;

                    int queuePriority =
                        ((int)retryPriority * 1_000_000)
                        + sequenceNumber;

                    sequenceNumber++;

                    ThumbnailLoadRequest retryRequest =
                        new ThumbnailLoadRequest(
                            request.Item,
                            retryPriority,
                            request.Item.ThumbnailGeneration);

                    queuedPriorities[
                        request.Item] =
                        retryPriority;

                    request.Item.ThumbnailStatus =
                        ThumbnailStatus.Loading;

                    queue.Enqueue(
                        retryRequest,
                        queuePriority);

                    queueSignal.Release();
                }
            }
        }
    }

    /// <summary>
    /// Reads the thumbnail from the model file and updates the model item with
    /// the resulting image or an appropriate error state.
    /// </summary>
    /// <param name="request">
    /// The thumbnail request containing the model and generation that was
    /// current when the thumbnail was queued.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous thumbnail loading operation.
    /// </returns>
    private async Task LoadThumbnailAsync(
        ThumbnailLoadRequest request) {
        IoFileListItem item =
            request.Item;

        try {
            ThumbnailReadResult result =
                await Task.Run(
                    () =>
                        reader.ReadThumbnail(
                            item.FilePath));

            switch (result.Status) {
                case ThumbnailReadStatus.Loaded:

                    if (result.Data is null) {
                        item.TryApplyThumbnailResult(
                            request.ThumbnailGeneration,
                            null,
                            ThumbnailStatus.Error,
                            "The thumbnail data was empty.");

                        return;
                    }

                    BitmapImage thumbnail =
                        CreateBitmapImage(
                            result.Data);

                    item.TryApplyThumbnailResult(
                        request.ThumbnailGeneration,
                        thumbnail,
                        ThumbnailStatus.Loaded,
                        null);

                    break;

                case ThumbnailReadStatus.Missing:

                    item.TryApplyThumbnailResult(
                        request.ThumbnailGeneration,
                        null,
                        ThumbnailStatus.Missing,
                        null);

                    break;

                case ThumbnailReadStatus.InvalidFile:

                    item.TryApplyThumbnailResult(
                        request.ThumbnailGeneration,
                        null,
                        ThumbnailStatus.Error,
                        result.ErrorMessage
                        ?? "The .io file is not a valid ZIP archive.");

                    break;

                case ThumbnailReadStatus.Error:

                    item.TryApplyThumbnailResult(
                        request.ThumbnailGeneration,
                        null,
                        ThumbnailStatus.Error,
                        result.ErrorMessage
                        ?? "An unknown error occurred.");

                    break;

                default:

                    item.TryApplyThumbnailResult(
                        request.ThumbnailGeneration,
                        null,
                        ThumbnailStatus.Error,
                        "Unknown thumbnail status.");

                    break;
            }
        }
        catch (Exception exception) {
            item.TryApplyThumbnailResult(
                request.ThumbnailGeneration,
                null,
                ThumbnailStatus.Error,
                exception.Message);
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
        /// Gets the thumbnail generation that was current when the request
        /// was accepted.
        /// </summary>
        public int ThumbnailGeneration {
            get;
        }

        /// <summary>
        /// Initializes a new thumbnail load request.
        /// </summary>
        /// <param name="item">
        /// The model item whose thumbnail should be loaded.
        /// </param>
        /// <param name="priority">
        /// The priority assigned to the thumbnail request.
        /// </param>
        /// <param name="thumbnailGeneration">
        /// The thumbnail generation associated with the request.
        /// </param>
        public ThumbnailLoadRequest(
            IoFileListItem item,
            ThumbnailLoadPriority priority,
            int thumbnailGeneration) {
            Item =
                item;

            Priority =
                priority;

            ThumbnailGeneration =
                thumbnailGeneration;
        }
    }
}