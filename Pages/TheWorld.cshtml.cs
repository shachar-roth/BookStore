using BookBoutique.Models;
using BookBoutique.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookBoutique.Pages;

public class TheWorldModel : PageModel
{
    private readonly GalleryStore _galleryStore;

    public TheWorldModel(GalleryStore galleryStore)
    {
        _galleryStore = galleryStore;
    }

    public IReadOnlyList<GalleryImageItem> Items { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Items = await _galleryStore.GetPublishedAsync(HttpContext.RequestAborted);
    }
}
