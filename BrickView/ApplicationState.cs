namespace BrickView;

public sealed class ApplicationState {
    public double? WindowLeft { get; set; }

    public double? WindowTop { get; set; }

    public double? WindowWidth { get; set; }

    public double? WindowHeight { get; set; }

    public string? LastSelectedFolder { get; set; }

    public ThumbnailSizePreset? ThumbnailSizePreset { get; set; }

    public FileSortField SortField { get; set; } =
        FileSortField.FileName;

    public FileSortDirection SortDirection { get; set; } =
        FileSortDirection.Ascending;
}