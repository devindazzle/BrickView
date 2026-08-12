using System.IO;
using System.Windows.Media.Imaging;

namespace BrickView;

public class ThumbnailLoader
{
    private const int MaxConcurrentLoads = 4;

    private readonly SemaphoreSlim semaphore =
        new SemaphoreSlim(MaxConcurrentLoads);

    private readonly IoFileReader reader =
        new IoFileReader();

    public async Task LoadAsync(IoFileListItem item)
    {
        if (item.ThumbnailStatus != ThumbnailStatus.NotLoaded)
        {
            return;
        }

        item.ThumbnailStatus =
            ThumbnailStatus.Loading;

        await semaphore.WaitAsync();

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

                    item.Thumbnail =
                        CreateBitmapImage(
                            result.Data);

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
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static BitmapImage CreateBitmapImage(byte[] imageData)
    {
        using MemoryStream stream =
            new MemoryStream(imageData);

        BitmapImage image =
            new BitmapImage();

        image.BeginInit();
        image.CacheOption =
            BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();

        return image;
    }
}