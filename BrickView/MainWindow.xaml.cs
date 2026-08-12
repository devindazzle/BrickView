using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace BrickView;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void SelectFolder_Click(
        object sender,
        RoutedEventArgs e)
    {
        OpenFolderDialog dialog = new OpenFolderDialog
        {
            Title = "Pick Folder"
        };

        bool? result = dialog.ShowDialog();

        if (result == true)
        {
            string folder = dialog.FolderName;

            FolderText.Text = folder;

            await LoadIoFilesAsync(folder);
        }
    }

    private async Task LoadIoFilesAsync(string folder)
    {
        FileList.Items.Clear();

        string[] files = Directory.GetFiles(
            folder,
            "*.io",
            SearchOption.TopDirectoryOnly)
            .OrderBy(
                file => Path.GetFileName(file),
                StringComparer.OrdinalIgnoreCase)
            .ToArray();

        IoFileReader reader = new IoFileReader();

        List<IoFileListItem> items = new List<IoFileListItem>();

        foreach (string file in files)
        {
            string fileName = Path.GetFileNameWithoutExtension(file);

            IoFileListItem item = new IoFileListItem(
                fileName,
                file,
                null,
                null);

            items.Add(item);

            FileList.Items.Add(item);
        }

        await LoadThumbnailsAsync(items, reader);
    }

    private async Task LoadThumbnailsAsync(
        IEnumerable<IoFileListItem> items,
        IoFileReader reader)
    {
        List<Task> tasks = new List<Task>();

        foreach (IoFileListItem item in items)
        {
            if (item.HasError)
            {
                continue;
            }

            Task task = LoadThumbnailAsync(
                item,
                reader);

            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
    }

    private async Task LoadThumbnailAsync(
        IoFileListItem item,
        IoFileReader reader)
    {
        try
        {
            byte[]? thumbnailData = await Task.Run(
                () => reader.ReadThumbnail(item.FilePath));

            if (thumbnailData is null)
            {
                return;
            }

            BitmapImage thumbnail = CreateBitmapImage(
                thumbnailData);

            item.Thumbnail = thumbnail;
        }
        catch (Exception exception)
        {
            item.ErrorMessage = exception.Message;
        }
    }

    private BitmapImage CreateBitmapImage(byte[] imageData)
    {
        using (MemoryStream stream = new MemoryStream(imageData))
        {
            BitmapImage image = new BitmapImage();

            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();

            return image;
        }
    }

    private void FileList_MouseDoubleClick(
        object sender,
        MouseButtonEventArgs e)
    {
        if (FileList.SelectedItem is not IoFileListItem item)
        {
            return;
        }

        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = item.FilePath,
                    UseShellExecute = true
                });
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Could not open the file.\n\n{exception.Message}",
                "BrickView",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}