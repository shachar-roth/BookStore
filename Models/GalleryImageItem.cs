namespace BookBoutique.Models;

public sealed class GalleryImageItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;

    public string Details { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public bool IsPublished { get; set; } = true;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public string PublicUrl => $"/media/gallery/{FileName}";
}
