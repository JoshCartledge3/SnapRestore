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
    [ObservableProperty] private string? _jsonPath;
    [ObservableProperty] private string? _outputPath;
    [ObservableProperty] private bool _isProcessing;
    [ObservableProperty] private bool _isProcessComplete;
    
    [ObservableProperty] private bool _isMemoriesDragOver;
    [ObservableProperty] private bool _isMemoriesDropZonePointerOver;
    [ObservableProperty] private bool _isJsonDragOver;
    [ObservableProperty] private bool _isJsonDropZonePointerOver;
    
    private SnapchatExportAnalysis? _analysis;
    private CancellationTokenSource? _processingCancellationTokenSource;
    private bool _processTaskActive;
    private string? _lastOutputFolder;

    #region File Drop Zone

    public IBrush MemoriesDropZoneBorderBrush => new SolidColorBrush(
        IsMemoriesDragOver || IsMemoriesDropZonePointerOver
            ? Color.Parse("#FEE207")
            : Color.Parse("#E8E8E8"));

    public double MemoriesDropZoneOpacity => IsMemoriesDragOver || IsMemoriesDropZonePointerOver ? 0.65 : 1;

    public IBrush JsonDropZoneBorderBrush => new SolidColorBrush(
        IsJsonDragOver || IsJsonDropZonePointerOver
            ? Color.Parse("#FEE207")
            : Color.Parse("#E8E8E8"));

    public double JsonDropZoneOpacity => IsJsonDragOver || IsJsonDropZonePointerOver ? 0.65 : 1;

    public bool CanChooseOutputFolder => _analysis?.IsValid == true && !IsProcessing;

    public bool CanProcess =>
        _analysis?.IsValid == true
        && !string.IsNullOrWhiteSpace(OutputPath);

    public bool CanUsePrimaryButton =>
        IsProcessing
        || IsProcessComplete
        || (CanProcess && !_processTaskActive);

    public bool IsPrimaryActionVisible => !IsProcessComplete;

    public bool IsFolderActionVisible => IsProcessComplete;

    public string PrimaryButtonText
    {
        get
        {
            if (IsProcessing)
                return "✕  Cancel";

            return IsProcessComplete
                ? "Go to Folder"
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
        RefreshAnalysis();
        OnPropertyChanged(nameof(CanChooseOutputFolder));
        OnPropertyChanged(nameof(CanProcess));
        NotifyPrimaryButtonStateChanged();
    }

    partial void OnJsonPathChanged(string? value)
    {
        ResetCompletionState();
        RefreshAnalysis();
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

    partial void OnIsMemoriesDragOverChanged(bool value)
    {
        OnPropertyChanged(nameof(MemoriesDropZoneBorderBrush));
        OnPropertyChanged(nameof(MemoriesDropZoneOpacity));
    }

    partial void OnIsMemoriesDropZonePointerOverChanged(bool value)
    {
        OnPropertyChanged(nameof(MemoriesDropZoneBorderBrush));
        OnPropertyChanged(nameof(MemoriesDropZoneOpacity));
    }

    partial void OnIsJsonDragOverChanged(bool value)
    {
        OnPropertyChanged(nameof(JsonDropZoneBorderBrush));
        OnPropertyChanged(nameof(JsonDropZoneOpacity));
    }

    partial void OnIsJsonDropZonePointerOverChanged(bool value)
    {
        OnPropertyChanged(nameof(JsonDropZoneBorderBrush));
        OnPropertyChanged(nameof(JsonDropZoneOpacity));
    }

    public void MemoriesDragEnter()
    {
        IsMemoriesDragOver = true;
    }

    public void MemoriesDragLeave()
    {
        IsMemoriesDragOver = false;
    }

    public void MemoriesDropZonePointerEnter()
    {
        IsMemoriesDropZonePointerOver = true;
    }

    public void MemoriesDropZonePointerLeave()
    {
        IsMemoriesDropZonePointerOver = false;
    }

    public void JsonDragEnter()
    {
        IsJsonDragOver = true;
    }

    public void JsonDragLeave()
    {
        IsJsonDragOver = false;
    }

    public void JsonDropZonePointerEnter()
    {
        IsJsonDropZonePointerOver = true;
    }

    public void JsonDropZonePointerLeave()
    {
        IsJsonDropZonePointerOver = false;
    }

    #endregion

    public void LoadMemoriesFolderPath(string path)
    {
        ExportPath = path;
    }

    public void LoadJsonFilePath(string path)
    {
        JsonPath = path;
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
        JsonPath = null;
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
        OnPropertyChanged(nameof(IsPrimaryActionVisible));
        OnPropertyChanged(nameof(IsFolderActionVisible));
        OnPropertyChanged(nameof(PrimaryButtonText));
        OnPropertyChanged(nameof(PrimaryButtonHelpText));
    }

    private void RefreshAnalysis()
    {
        ProcessedFiles = 0;
        FailedFiles = 0;
        ProgressPercentage = 0;
        IsProcessComplete = false;
        _lastOutputFolder = null;

        var hasMemoriesPath = !string.IsNullOrWhiteSpace(ExportPath);
        var hasJsonPath = !string.IsNullOrWhiteSpace(JsonPath);

        FolderStatus = hasMemoriesPath ? ExportPath! : "Not loaded";
        JsonStatus = hasJsonPath ? JsonPath! : "Not selected";

        if (!hasMemoriesPath)
        {
            _analysis = null;
            FilesToProcess = 0;
            MemoriesCount = "–";
            ProgressStatus = hasJsonPath
                ? "Select memories folder"
                : "Idle";
            return;
        }

        _analysis = snapchatExportService.Analyse(ExportPath!, JsonPath ?? string.Empty);
        FilesToProcess = _analysis.MainMediaCount;
        MemoriesCount = _analysis.MainMediaCount.ToString();
        ProgressStatus = hasJsonPath
            ? _analysis.StatusMessage
            : "Select JSON file";
    }

    [RelayCommand]
    private async Task SelectOutputFolder()
    {
        await Task.CompletedTask;
    }
}
