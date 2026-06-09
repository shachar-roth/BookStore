using BookBoutique.Models;
using BookBoutique.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookBoutique.Pages;

public class PodcastModel : PageModel
{
    private readonly PodcastStore _podcastStore;

    public PodcastModel(PodcastStore podcastStore)
    {
        _podcastStore = podcastStore;
    }

    public IReadOnlyList<PodcastItem> Items { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Items = await _podcastStore.GetPublishedAsync(HttpContext.RequestAborted);
    }
}
