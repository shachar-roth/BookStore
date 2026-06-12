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
        BookViewer = new PdfBookViewerModel
        {
            PdfUrl = BookViewer.PdfUrl,
            PageImageUrls = BookViewer.PageImageUrls,
            EndPage = new PdfBookViewerEndPageModel
            {
                Title = "זה סוף הפרקים החינמיים",
                Text = "כדי להמשיך לקרוא את עין המלך, אפשר להזמין את הספר המלא",
                ButtonText = "הזמן עכשיו",
                ButtonUrl = "/Order"
            }
        };
    }
}
