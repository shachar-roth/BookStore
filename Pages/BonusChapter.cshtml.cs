using BookBoutique.Models;
using BookBoutique.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookBoutique.Pages;

public class BonusChapterModel : PageModel
{
    private readonly BonusContentStore _bonusContentStore;

    public BonusChapterModel(BonusContentStore bonusContentStore)
    {
        _bonusContentStore = bonusContentStore;
    }

    public BonusContentItem? Item { get; private set; }

    public string PdfUrl => Item is null ? string.Empty : Url.Content(Item.PdfPublicUrl);

    public PdfBookViewerModel BookViewer => new()
    {
        PdfUrl = PdfUrl,
        PageImageUrls = Item?.PdfPageImagePublicUrls.Select(url => Url.Content(url) ?? url).ToList() ?? []
    };

    public async Task OnGetAsync(string slug)
    {
        var items = await _bonusContentStore.GetPublishedAsync(HttpContext.RequestAborted);
        Item = items.FirstOrDefault(item => CreateSlug(item) == slug);
        if (Item is not null)
        {
            Item = await _bonusContentStore.EnsurePageImagesAsync(Item.Id, HttpContext.RequestAborted);
        }
    }

    public static string CreateSlug(BonusContentItem item) =>
        $"chapter-{item.Id:N}"[..16];
}
