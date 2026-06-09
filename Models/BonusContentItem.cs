using System.Text.Json.Serialization;

namespace BookBoutique.Models;

public sealed class BonusContentItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string PdfFileName { get; set; } = string.Empty;

    public string OriginalPdfFileName { get; set; } = string.Empty;

    public List<string> PdfPageImageFileNames { get; set; } = [];

    public bool IsPublished { get; set; } = true;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public string PdfPublicUrl => $"/media/bonus-pdfs/{PdfFileName}";

    [JsonIgnore]
    public IReadOnlyList<string> PdfPageImagePublicUrls =>
        PdfPageImageFileNames
            .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
            .Select(fileName => $"/media/bonus-pages/{fileName}")
            .ToList();
}
