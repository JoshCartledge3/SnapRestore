namespace SnapRestore.Models;

public class ProcessingProgress
{
    public int TotalFiles { get; init; }
    public int ProcessedFiles { get; init; }
    public int FailedFiles { get; init; }

    public double Percentage => TotalFiles == 0 ? 0 : (double)ProcessedFiles / TotalFiles * 100;
}