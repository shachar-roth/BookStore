using BookBoutique.Models;
using BookBoutique.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookBoutique.Pages;

public class BonusModel : PageModel
{
    private readonly BonusContentStore _bonusContentStore;

    public BonusModel(BonusContentStore bonusContentStore)
    {
        _bonusContentStore = bonusContentStore;
    }

    public IReadOnlyList<BonusContentItem> Items { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Items = await _bonusContentStore.GetPublishedAsync(HttpContext.RequestAborted);
    }
}
