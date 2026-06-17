using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using System.Linq;
using SnapRestore.ViewModels;

namespace SnapRestore.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private static readonly Cursor HoverCursor = new(StandardCursorType.Hand);
    private static readonly Cursor DragOverCursor = new(StandardCursorType.DragMove);
    private static readonly Cursor NoDropCursor = new(StandardCursorType.No);

    private void OnMemoriesPointerEntered(object? sender, PointerEventArgs e)
    {
        ViewModel?.MemoriesDropZonePointerEnter();
    }

    private void OnMemoriesPointerExited(object? sender, PointerEventArgs e)
    {
        ViewModel?.MemoriesDropZonePointerLeave();
        MemoriesDropZone.Cursor = HoverCursor;
    }

    private async void OnMemoriesPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ViewModel?.IsMemoriesDragOver == true || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        var files = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Snapchat memories folder",
            AllowMultiple = false
        });

        if (files.Count == 0)
            return;

        var path = files[0].Path.LocalPath;
        if (!string.IsNullOrWhiteSpace(path))
            ViewModel?.LoadMemoriesFolderPath(path);

        e.Handled = true;
    }

    private void OnMemoriesDragEnter(object? sender, DragEventArgs e)
    {
        ViewModel?.MemoriesDragEnter();
        SetDropEffectsAndCursor(e, MemoriesDropZone);
    }

    private void OnMemoriesDragOver(object? sender, DragEventArgs e) => SetDropEffectsAndCursor(e, MemoriesDropZone);

    private void OnMemoriesDragLeave(object? sender, DragEventArgs e)
    {
        ViewModel?.MemoriesDragLeave();
        MemoriesDropZone.Cursor = HoverCursor;
    }

    private void OnMemoriesDrop(object? sender, DragEventArgs e)
    {
        ViewModel?.MemoriesDragLeave();
        MemoriesDropZone.Cursor = HoverCursor;

        var files = e.DataTransfer.TryGetFiles();
        if (files is not { Length: > 0 })
            return;

        ViewModel?.LoadMemoriesFolderPath(files[0].Path.LocalPath);
        e.Handled = true;
    }

    private void OnJsonPointerEntered(object? sender, PointerEventArgs e)
    {
        ViewModel?.JsonDropZonePointerEnter();
    }

    private void OnJsonPointerExited(object? sender, PointerEventArgs e)
    {
        ViewModel?.JsonDropZonePointerLeave();
        JsonDropZone.Cursor = HoverCursor;
    }

    private async void OnJsonPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ViewModel?.IsJsonDragOver == true || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select JSON File",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("JSON files")
                {
                    Patterns = ["*.json"],
                    AppleUniformTypeIdentifiers = ["public.json"],
                    MimeTypes = ["application/json"]
                }
            ]
        });

        if (files.Count == 0)
            return;

        var path = files[0].Path.LocalPath;
        if (!string.IsNullOrWhiteSpace(path))
            ViewModel?.LoadJsonFilePath(path);

        e.Handled = true;
    }

    private void OnJsonDragEnter(object? sender, DragEventArgs e)
    {
        ViewModel?.JsonDragEnter();
        SetDropEffectsAndCursor(e, JsonDropZone);
    }

    private void OnJsonDragOver(object? sender, DragEventArgs e) => SetDropEffectsAndCursor(e, JsonDropZone);

    private void OnJsonDragLeave(object? sender, DragEventArgs e)
    {
        ViewModel?.JsonDragLeave();
        JsonDropZone.Cursor = HoverCursor;
    }

    private void OnJsonDrop(object? sender, DragEventArgs e)
    {
        ViewModel?.JsonDragLeave();
        JsonDropZone.Cursor = HoverCursor;

        var files = e.DataTransfer.TryGetFiles();
        if (files is not { Length: > 0 })
            return;

        ViewModel?.LoadJsonFilePath(files[0].Path.LocalPath);
        e.Handled = true;
    }

    private async void OnSelectOutputFolder(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select output folder",
            AllowMultiple = false
        });

        if (folders.Count == 0)
            return;

        var path = folders[0].Path.LocalPath;
        if (!string.IsNullOrWhiteSpace(path))
            ViewModel?.SelectOutputPath(path);

        e.Handled = true;
    }

    private static void SetDropEffectsAndCursor(DragEventArgs e, Control dropZone)
    {
        var hasFiles = e.DataTransfer.TryGetFiles()?.Any() == true;
        e.DragEffects = hasFiles ? DragDropEffects.Copy : DragDropEffects.None;
        dropZone.Cursor = hasFiles ? DragOverCursor : NoDropCursor;
        e.Handled = true;
    }
}
