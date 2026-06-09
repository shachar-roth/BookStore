using System.ComponentModel.DataAnnotations;
using BookBoutique.Models;
using BookBoutique.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookBoutique.Pages.Admin;

public class GalleryModel : PageModel
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
    private readonly GalleryStore _galleryStore;

    public GalleryModel(GalleryStore galleryStore)
    {
        _galleryStore = galleryStore;
    }

    [BindProperty]
    public EditorInput Editor { get; set; } = new();

    public IReadOnlyList<GalleryImageItem> Items { get; private set; } = [];

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

        if (!string.IsNullOrWhiteSpace(Editor.Details) && Editor.Details.Length > 600)
        {
            ModelState.AddModelError("Editor.Details", "התיאור יכול להכיל עד 600 תווים.");
        }

        var uploadedFile = Editor.ImageFile;
        if (uploadedFile is not null)
        {
            var extension = Path.GetExtension(uploadedFile.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
            {
                ModelState.AddModelError("Editor.ImageFile", "אפשר להעלות רק קבצי תמונה נתמכים.");
            }
        }

        if (ModelState.ErrorCount > 0)
        {
            await LoadPageAsync(Editor.Id == Guid.Empty ? null : Editor.Id);
            return Page();
        }

        var item = new GalleryImageItem
        {
            Id = Editor.Id == Guid.Empty ? Guid.NewGuid() : Editor.Id,
            Title = Editor.Title.Trim(),
            Details = Editor.Details?.Trim() ?? string.Empty,
            IsPublished = Editor.IsPublished
        };

        await _galleryStore.SaveAsync(item, uploadedFile, HttpContext.RequestAborted);
        StatusMessage = "הפריט נשמר בהצלחה.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        await _galleryStore.DeleteAsync(id, HttpContext.RequestAborted);
        StatusMessage = "הפריט נמחק.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEditAsync(Guid id)
    {
        await LoadPageAsync(id);
        return Page();
    }

    private async Task LoadPageAsync(Guid? editId)
    {
        Items = await _galleryStore.GetAllAsync(HttpContext.RequestAborted);

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
            Details = item.Details,
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

        [StringLength(600)]
        public string? Details { get; set; }

        public IFormFile? ImageFile { get; set; }

        public string? ExistingFileName { get; set; }

        public bool IsPublished { get; set; }
    }
}
