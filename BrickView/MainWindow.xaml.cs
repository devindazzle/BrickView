using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace BrickView;

public partial class MainWindow : Window
{
    private readonly ThumbnailLoader thumbnailLoader;


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
    }


    private void ThumbnailContainer_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        if (element.DataContext is not IoFileListItem item)
        {
            return;
        }

        _ = thumbnailLoader.LoadAsync(
            item,
            ThumbnailLoadPriority.Visible);
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

        System.Diagnostics.Debug.WriteLine(
            $"Viewport: {firstVisibleIndex}-{lastVisibleIndex}");
    }
}