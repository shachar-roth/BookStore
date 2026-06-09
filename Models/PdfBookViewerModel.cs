namespace BookBoutique.Models;

public sealed class PdfBookViewerModel
{
    public string PdfUrl { get; init; } = string.Empty;

    public IReadOnlyList<string> PageImageUrls { get; init; } = [];
}
