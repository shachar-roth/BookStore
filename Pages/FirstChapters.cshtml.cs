using BookBoutique.Models;
using BookBoutique.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookBoutique.Pages;

public class FirstChaptersModel : PageModel
{
    private readonly FirstChaptersStore _firstChaptersStore;

    public FirstChaptersModel(FirstChaptersStore firstChaptersStore)
    {
        _firstChaptersStore = firstChaptersStore;
    }

    public PdfBookViewerModel BookViewer { get; private set; } = new();

    public async Task OnGetAsync()
    {
        BookViewer = await _firstChaptersStore.GetBookViewerAsync(HttpContext.RequestAborted);
    }
}
