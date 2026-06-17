using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SnapRestore.Models;
using SnapRestore.Services.Abstraction;

namespace SnapRestore.ViewModels;

public partial class MainWindowViewModel(ISnapchatExportService snapchatExportService, IMemoryProcessingService memoryProcessingService) : ViewModelBase
{
    [ObservableProperty] private string _folderStatus = "Not loaded";
    [ObservableProperty] private string _memoriesCount = "–";
    [ObservableProperty] private string _jsonStatus = "–";

    [ObservableProperty] private int _filesToProcess;
    [ObservableProperty] private int _processedFiles;
    [ObservableProperty] private int _failedFiles;
    [ObservableProperty] private double _progressPercentage;

    [ObservableProperty] private string _progressStatus = "Idle";
    [ObservableProperty] private string? _exportPath;
    [ObservableProperty] private string? _outputPath;
    [ObservableProperty] private bool _isProcessing;
    [ObservableProperty] private bool _isProcessComplete;
    
    [ObservableProperty] private bool _isDragOver;
    [ObservableProperty] private bool _isDropZonePointerOver;
    
    private SnapchatExportAnalysis? _analysis;
    private CancellationTokenSource? _processingCancellationTokenSource;
    private bool _processTaskActive;
    private string? _lastOutputFolder;

    #region File Drop Zone

    public IBrush DropZoneBorderBrush => new SolidColorBrush(
        IsDragOver || IsDropZonePointerOver
            ? Color.Parse("#FEE207")
            : Color.Parse("#E8E8E8"));

    public double DropZoneOpacity => IsDragOver || IsDropZonePointerOver ? 0.65 : 1;

    public bool CanChooseOutputFolder => !string.IsNullOrWhiteSpace(ExportPath) && !IsProcessing;

    public bool CanProcess =>
        !string.IsNullOrWhiteSpace(ExportPath)
        && !string.IsNullOrWhiteSpace(OutputPath);

    public bool CanUsePrimaryButton =>
        IsProcessing
        || IsProcessComplete
        || (CanProcess && !_processTaskActive);

    public string PrimaryButtonText
    {
        get
        {
            if (IsProcessing)
                return "✕  Cancel";

            return IsProcessComplete
                ? "📂  Go to Folder"
                : "▶  Go";
        }
    }

    public string PrimaryButtonHelpText
    {
        get
        {
            if (IsProcessing)
                return "Stop after the current file";

            return IsProcessComplete
                ? "Open processed output"
                : "Start geotagging process";
        }
    }

    public string OutputStatus =>
        string.IsNullOrWhiteSpace(OutputPath)
            ? "Not selected"
            : OutputPath;

    partial void OnExportPathChanged(string? value)
    {
        ResetCompletionState();
        OnPropertyChanged(nameof(CanChooseOutputFolder));
        OnPropertyChanged(nameof(CanProcess));
        NotifyPrimaryButtonStateChanged();
    }

    partial void OnOutputPathChanged(string? value)
    {
        ResetCompletionState();
        OnPropertyChanged(nameof(OutputStatus));
        OnPropertyChanged(nameof(CanProcess));
        NotifyPrimaryButtonStateChanged();
    }

    partial void OnIsProcessingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanChooseOutputFolder));
        NotifyPrimaryButtonStateChanged();
    }

    partial void OnIsProcessCompleteChanged(bool value)
    {
        NotifyPrimaryButtonStateChanged();
    }

    partial void OnIsDragOverChanged(bool value)
    {
        OnPropertyChanged(nameof(DropZoneBorderBrush));
        OnPropertyChanged(nameof(DropZoneOpacity));
    }

    partial void OnIsDropZonePointerOverChanged(bool value)
    {
        OnPropertyChanged(nameof(DropZoneBorderBrush));
        OnPropertyChanged(nameof(DropZoneOpacity));
    }

    public void DragEnter()
    {
        IsDragOver = true;
    }

    public void DragLeave()
    {
        IsDragOver = false;
    }

    public void DropZonePointerEnter()
    {
        IsDropZonePointerOver = true;
    }

    public void DropZonePointerLeave()
    {
        IsDropZonePointerOver = false;
    }

    #endregion

    public void LoadDroppedPath(string path)
    {
        _analysis = snapchatExportService.Analyse(path);

        FolderStatus = _analysis.ExportRootPath ?? _analysis.OriginalPath;
        FilesToProcess = _analysis.MainMediaCount;
        ProcessedFiles = 0;
        FailedFiles = 0;
        ProgressPercentage = 0;
        JsonStatus = _analysis.JsonFound ? "Found" : _analysis.IsZip ? "Ready to extract" : "Missing";
        MemoriesCount = _analysis.IsZip ? "Unknown" : _analysis.MainMediaCount.ToString();
        ProgressStatus = _analysis.StatusMessage;
        ExportPath = _analysis.IsValid ? _analysis.ExportRootPath ?? _analysis.OriginalPath : null;
        IsProcessComplete = false;
        _lastOutputFolder = null;

        if (!_analysis.IsValid)
        {
            FilesToProcess = 0;
            MemoriesCount = _analysis.IsZip ? "Unknown" : "0";
            OutputPath = null;
        }
    }

    public void SelectOutputPath(string path)
    {
        OutputPath = path;
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task Go()
    {
        if (IsProcessing)
        {
            CancelProcessing();
            return;
        }

        if (IsProcessComplete)
        {
            OpenLastOutputFolder();
            return;
        }

        if (_analysis is null || string.IsNullOrWhiteSpace(OutputPath))
            return;

        if (_processTaskActive)
            return;

        _processTaskActive = true;
        NotifyPrimaryButtonStateChanged();

        _processingCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _processingCancellationTokenSource.Token;

        ProcessedFiles = 0;
        FailedFiles = 0;
        ProgressPercentage = 0;
        ProgressStatus = "Processing...";
        IsProcessComplete = false;
        IsProcessing = true;
        _lastOutputFolder = null;

        try
        {
            var outputFolder = await memoryProcessingService.ProcessAsync(
                _analysis,
                OutputPath,
                new Progress<ProcessingProgress>(OnProgressChanged),
                cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                ProgressStatus = "Cancelled";
                return;
            }

            _lastOutputFolder = outputFolder;
            ProgressStatus = "Complete";
            IsProcessComplete = !string.IsNullOrWhiteSpace(_lastOutputFolder);
        }
        catch (OperationCanceledException)
        {
            ProgressStatus = "Cancelled";
        }
        catch (Exception ex)
        {
            ProgressStatus = $"Failed: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
            _processTaskActive = false;
            _processingCancellationTokenSource?.Dispose();
            _processingCancellationTokenSource = null;
            NotifyPrimaryButtonStateChanged();
        }
    }
    
    private void OnProgressChanged(ProcessingProgress progress)
    {
        FilesToProcess = progress.TotalFiles;
        ProcessedFiles = progress.ProcessedFiles;
        FailedFiles = progress.FailedFiles;
        ProgressPercentage = progress.Percentage;
    }

    private void CancelProcessing()
    {
        _processingCancellationTokenSource?.Cancel();
        IsProcessing = false;
        ResetWorkflowState("Cancelled");
    }

    private void ResetWorkflowState(string progressStatus)
    {
        _analysis = null;
        _lastOutputFolder = null;

        ExportPath = null;
        OutputPath = null;
        FolderStatus = "Not loaded";
        MemoriesCount = "–";
        JsonStatus = "–";
        FilesToProcess = 0;
        ProcessedFiles = 0;
        FailedFiles = 0;
        ProgressPercentage = 0;
        ProgressStatus = progressStatus;
        IsProcessComplete = false;

        OnPropertyChanged(nameof(CanChooseOutputFolder));
        OnPropertyChanged(nameof(CanProcess));
        OnPropertyChanged(nameof(OutputStatus));
        NotifyPrimaryButtonStateChanged();
    }

    private void OpenLastOutputFolder()
    {
        if (string.IsNullOrWhiteSpace(_lastOutputFolder))
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _lastOutputFolder,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ProgressStatus = $"Could not open folder: {ex.Message}";
        }
    }

    private void ResetCompletionState()
    {
        if (IsProcessing)
            return;

        IsProcessComplete = false;
        _lastOutputFolder = null;
    }

    private void NotifyPrimaryButtonStateChanged()
    {
        OnPropertyChanged(nameof(CanUsePrimaryButton));
        OnPropertyChanged(nameof(PrimaryButtonText));
        OnPropertyChanged(nameof(PrimaryButtonHelpText));
    }

    [RelayCommand]
    private async Task SelectOutputFolder()
    {
        await Task.CompletedTask;
    }
}
