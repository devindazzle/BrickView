// -----------------------------------------------------------------------------
// MainWindow.FileActions.cs
//
// Contains the model opening, Explorer integration and clipboard actions for BrickView's MainWindow partial class.
// This file is an organizational split only; application behavior is unchanged.
// -----------------------------------------------------------------------------

using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BrickView;

public partial class MainWindow : Window {

    /// <summary>
    /// Opens the model file represented by the clicked thumbnail.
    /// </summary>
    /// <param name="sender">The thumbnail element that was clicked.</param>
    /// <param name="e">Mouse event data supplied by WPF.</param>
    private void Thumbnail_MouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e) {
        if (sender is not FrameworkElement element) {
            return;
        }

        if (element.DataContext
            is not IoFileListItem item) {
            return;
        }

        OpenFile(
            item);

        e.Handled = true;
    }

    /// <summary>
    /// Opens the model associated with the context menu in the default application.
    /// </summary>
    /// <param name="sender">The context-menu item that was clicked.</param>
    /// <param name="e">Event data supplied by WPF.</param>
    private void OpenInStudio_Click(
        object sender,
        RoutedEventArgs e) {
        if (sender is not MenuItem menuItem) {
            return;
        }

        if (menuItem.Parent is not ContextMenu contextMenu) {
            return;
        }

        if (contextMenu.PlacementTarget
            is not FrameworkElement element) {
            return;
        }

        if (element.DataContext
            is not IoFileListItem item) {
            return;
        }

        OpenFile(
            item);
    }

    /// <summary>
    /// Opens the specified model file using the Windows shell.
    /// </summary>
    /// <param name="item">The model-list item whose file should be opened.</param>
    private void OpenFile(
        IoFileListItem item) {
        if (item.HasError) {
            return;
        }

        try {
            Process.Start(
                new ProcessStartInfo {
                    FileName = item.FilePath,
                    UseShellExecute = true
                });
        }
        catch (Exception exception) {
            MessageBox.Show(
                $"Could not open the file.\n\n{exception.Message}",
                "BrickView",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Opens Windows File Explorer with the selected model file highlighted.
    /// </summary>
    /// <param name="sender">The context-menu item that was clicked.</param>
    /// <param name="e">Event data supplied by WPF.</param>
    private void ShowInFileExplorer_Click(
        object sender,
        RoutedEventArgs e) {
        if (sender is not MenuItem menuItem) {
            return;
        }

        if (menuItem.Parent is not ContextMenu contextMenu) {
            return;
        }

        if (contextMenu.PlacementTarget
            is not FrameworkElement element) {
            return;
        }

        if (element.DataContext
            is not IoFileListItem item) {
            return;
        }

        if (!File.Exists(
                item.FilePath)) {
            MessageBox.Show(
                "The file no longer exists.",
                "BrickView",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        try {
            Process.Start(
                new ProcessStartInfo {
                    FileName = "explorer.exe",
                    Arguments =
                        $"/select,\"{item.FilePath}\"",
                    UseShellExecute = true
                });
        }
        catch (Exception exception) {
            MessageBox.Show(
                $"Could not open File Explorer.\n\n{exception.Message}",
                "BrickView",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Copies the full path of the selected model file to the Windows clipboard.
    /// </summary>
    /// <param name="sender">The context-menu item that was clicked.</param>
    /// <param name="e">Event data supplied by WPF.</param>
    private void CopyFilePath_Click(
        object sender,
        RoutedEventArgs e) {
        if (sender is not MenuItem menuItem) {
            return;
        }

        if (menuItem.Parent is not ContextMenu contextMenu) {
            return;
        }

        if (contextMenu.PlacementTarget
            is not FrameworkElement element) {
            return;
        }

        if (element.DataContext
            is not IoFileListItem item) {
            return;
        }

        try {
            Clipboard.SetText(
                item.FilePath);
        }
        catch (Exception exception) {
            MessageBox.Show(
                $"Could not copy the file path.\n\n{exception.Message}",
                "BrickView",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Copies the file name of the selected model to the Windows clipboard.
    /// </summary>
    /// <param name="sender">The context-menu item that was clicked.</param>
    /// <param name="e">Event data supplied by WPF.</param>
    private void CopyFileName_Click(
        object sender,
        RoutedEventArgs e) {
        if (sender is not MenuItem menuItem) {
            return;
        }

        if (menuItem.Parent is not ContextMenu contextMenu) {
            return;
        }

        if (contextMenu.PlacementTarget
            is not FrameworkElement element) {
            return;
        }

        if (element.DataContext
            is not IoFileListItem item) {
            return;
        }

        try {
            Clipboard.SetText(
                Path.GetFileName(
                    item.FilePath));
        }
        catch (Exception exception) {
            MessageBox.Show(
                $"Could not copy the file name.\n\n{exception.Message}",
                "BrickView",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

}