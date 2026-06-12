namespace BookBoutique.Models;

public sealed class PdfBookViewerModel
{
    public string PdfUrl { get; init; } = string.Empty;

    public IReadOnlyList<string> PageImageUrls { get; init; } = [];

    public PdfBookViewerEndPageModel? EndPage { get; init; }
}

public sealed class PdfBookViewerEndPageModel
{
    public string Title { get; init; } = string.Empty;

    public string Text { get; init; } = string.Empty;

    public string ButtonText { get; init; } = string.Empty;

    public string ButtonUrl { get; init; } = string.Empty;
}
