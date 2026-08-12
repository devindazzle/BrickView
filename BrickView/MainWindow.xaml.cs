using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace BrickView;

public partial class MainWindow : Window
{
    
    private readonly ThumbnailLoader thumbnailLoader;

    private string? currentFolder;


    public MainWindow()
    {
        InitializeComponent();

        thumbnailLoader = new ThumbnailLoader();

        FileList.AddHandler(
            VirtualizingWrapPanel.ViewportChangedEvent,
            new RoutedEventHandler(FileList_ViewportChanged));
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

            currentFolder = folder;

            FolderText.Text = folder;

            await LoadIoFilesAsync(folder);
        }
    }


    private async void RefreshView_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(currentFolder))
        {
            return;
        }

        if (!Directory.Exists(currentFolder))
        {
            MessageBox.Show(
                "The selected folder no longer exists.",
                "BrickView",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            currentFolder = null;
            FolderText.Text = string.Empty;

            return;
        }

        await LoadIoFilesAsync(currentFolder);
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


    private void FileList_ViewportChanged(object sender, RoutedEventArgs e)
    {
        if (e is not ViewportChangedEventArgs viewportEventArgs)
        {
            return;
        }

        int firstVisibleIndex =
            viewportEventArgs.FirstVisibleIndex;

        int lastVisibleIndex =
            viewportEventArgs.LastVisibleIndex;

        int itemCount =
            FileList.Items.Count;

        if (itemCount == 0)
        {
            return;
        }

        int clampedFirstIndex =
            Math.Max(
                0,
                Math.Min(
                    firstVisibleIndex,
                    itemCount - 1));

        int clampedLastIndex =
            Math.Max(
                clampedFirstIndex,
                Math.Min(
                    lastVisibleIndex,
                    itemCount - 1));

        for (
            int index = clampedFirstIndex;
            index <= clampedLastIndex;
            index++)
        {
            if (FileList.Items[index]
                is IoFileListItem item)
            {
                thumbnailLoader.LoadAsync(
                    item,
                    ThumbnailLoadPriority.Visible);
            }
        }

        const int preloadCount = 8;

        int preloadStart =
            clampedLastIndex + 1;

        int preloadEnd =
            Math.Min(
                itemCount - 1,
                preloadStart + preloadCount - 1);

        for (
            int index = preloadStart;
            index <= preloadEnd;
            index++)
        {
            if (FileList.Items[index]
                is IoFileListItem item)
            {
                thumbnailLoader.LoadAsync(
                    item,
                    ThumbnailLoadPriority.Preload);
            }
        }
    }
}