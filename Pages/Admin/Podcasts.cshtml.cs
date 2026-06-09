using System.ComponentModel.DataAnnotations;
using BookBoutique.Models;
using BookBoutique.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookBoutique.Pages.Admin;

public class PodcastsModel : PageModel
{
    private static readonly string[] AllowedExtensions = [".mp3", ".m4a", ".wav", ".mp4", ".aac"];
    private readonly PodcastStore _podcastStore;

    public PodcastsModel(PodcastStore podcastStore)
    {
        _podcastStore = podcastStore;
    }

    [BindProperty]
    public EditorInput Editor { get; set; } = new();

    public IReadOnlyList<PodcastItem> Items { get; private set; } = [];

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid? editId = null)
    {
        await LoadPageAsync(editId);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        ModelState.Clear();

        if (string.IsNullOrWhiteSpace(Editor.Title))
        {
            ModelState.AddModelError("Editor.Title", "צריך להזין כותרת.");
        }

        if (Editor.Title.Length > 120)
        {
            ModelState.AddModelError("Editor.Title", "הכותרת יכולה להכיל עד 120 תווים.");
        }

        var uploadedFile = Editor.AudioFile;
        if (Editor.Id == Guid.Empty && uploadedFile is null)
        {
            ModelState.AddModelError("Editor.AudioFile", "צריך להעלות קובץ אודיו.");
        }

        if (uploadedFile is not null)
        {
            var extension = Path.GetExtension(uploadedFile.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
            {
                ModelState.AddModelError("Editor.AudioFile", "אפשר להעלות רק קבצי אודיו נתמכים.");
            }
        }

        if (ModelState.ErrorCount > 0)
        {
            await LoadPageAsync(Editor.Id == Guid.Empty ? null : Editor.Id);
            return Page();
        }

        var item = new PodcastItem
        {
            Id = Editor.Id == Guid.Empty ? Guid.NewGuid() : Editor.Id,
            Title = Editor.Title.Trim(),
            IsPublished = Editor.IsPublished
        };

        await _podcastStore.SaveAsync(item, uploadedFile, HttpContext.RequestAborted);
        StatusMessage = "הפודקסט נשמר בהצלחה.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        await _podcastStore.DeleteAsync(id, HttpContext.RequestAborted);
        StatusMessage = "הפודקסט נמחק.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEditAsync(Guid id)
    {
        await LoadPageAsync(id);
        return Page();
    }

    private async Task LoadPageAsync(Guid? editId)
    {
        Items = await _podcastStore.GetAllAsync(HttpContext.RequestAborted);

        if (editId is null)
        {
            Editor = new EditorInput
            {
                IsPublished = true
            };
            return;
        }

        var item = Items.FirstOrDefault(existing => existing.Id == editId.Value);
        if (item is null)
        {
            Editor = new EditorInput
            {
                IsPublished = true
            };
            return;
        }

        Editor = new EditorInput
        {
            Id = item.Id,
            Title = item.Title,
            IsPublished = item.IsPublished,
            ExistingFileName = item.OriginalFileName
        };
    }

    public sealed class EditorInput
    {
        public Guid Id { get; set; }

        [Required]
        [StringLength(120)]
        public string Title { get; set; } = string.Empty;

        public IFormFile? AudioFile { get; set; }

        public string? ExistingFileName { get; set; }

        public bool IsPublished { get; set; }
    }
}
