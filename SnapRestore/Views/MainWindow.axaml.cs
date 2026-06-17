using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
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

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        ViewModel?.DropZonePointerEnter();
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        ViewModel?.DropZonePointerLeave();
        DropZone.Cursor = HoverCursor;
    }

    private async void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ViewModel?.IsDragOver == true || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        var files = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Snapchat export folder",
            AllowMultiple = false
        });

        if (files.Count == 0)
            return;

        var path = files[0].Path.LocalPath;
        if (!string.IsNullOrWhiteSpace(path))
            ViewModel?.LoadDroppedPath(path);

        e.Handled = true;
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        ViewModel?.DragEnter();
        SetDropEffectsAndCursor(e);
    }

    private void OnDragOver(object? sender, DragEventArgs e) => SetDropEffectsAndCursor(e);

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        ViewModel?.DragLeave();
        DropZone.Cursor = HoverCursor;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        ViewModel?.DragLeave();
        DropZone.Cursor = HoverCursor;

        var files = e.DataTransfer.TryGetFiles();
        if (files is not { Length: > 0 })
            return;

        ViewModel?.LoadDroppedPath(files[0].Path.LocalPath);
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

    private void SetDropEffectsAndCursor(DragEventArgs e)
    {
        var hasFiles = e.DataTransfer.TryGetFiles() is { Length: > 0 };
        e.DragEffects = hasFiles ? DragDropEffects.Copy : DragDropEffects.None;
        DropZone.Cursor = hasFiles ? DragOverCursor : NoDropCursor;
        e.Handled = true;
    }
}
