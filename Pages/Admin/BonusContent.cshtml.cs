using System.ComponentModel.DataAnnotations;
using BookBoutique.Models;
using BookBoutique.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookBoutique.Pages.Admin;

public class BonusContentModel : PageModel
{
    private const string PdfExtension = ".pdf";
    private readonly BonusContentStore _bonusContentStore;

    public BonusContentModel(BonusContentStore bonusContentStore)
    {
        _bonusContentStore = bonusContentStore;
    }

    [BindProperty]
    public EditorInput Editor { get; set; } = new();

    public IReadOnlyList<BonusContentItem> Items { get; private set; } = [];

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

        if (!string.IsNullOrWhiteSpace(Editor.Summary) && Editor.Summary.Length > 280)
        {
            ModelState.AddModelError("Editor.Summary", "התקציר יכול להכיל עד 280 תווים.");
        }

        var uploadedFile = Editor.PdfFile;
        if (Editor.Id == Guid.Empty && uploadedFile is null)
        {
            ModelState.AddModelError("Editor.PdfFile", "צריך להעלות קובץ PDF.");
        }

        if (uploadedFile is not null)
        {
            var extension = Path.GetExtension(uploadedFile.FileName).ToLowerInvariant();
            if (extension != PdfExtension)
            {
                ModelState.AddModelError("Editor.PdfFile", "אפשר להעלות רק קובץ PDF.");
            }
        }

        if (Editor.Id != Guid.Empty && uploadedFile is null)
        {
            var items = await _bonusContentStore.GetAllAsync(HttpContext.RequestAborted);
            var existing = items.FirstOrDefault(item => item.Id == Editor.Id);
            if (existing is null || string.IsNullOrWhiteSpace(existing.PdfFileName))
            {
                ModelState.AddModelError("Editor.PdfFile", "צריך להעלות קובץ PDF.");
            }
        }

        if (ModelState.ErrorCount > 0)
        {
            await LoadPageAsync(Editor.Id == Guid.Empty ? null : Editor.Id);
            return Page();
        }

        var item = new BonusContentItem
        {
            Id = Editor.Id == Guid.Empty ? Guid.NewGuid() : Editor.Id,
            Title = Editor.Title.Trim(),
            Summary = Editor.Summary?.Trim() ?? string.Empty,
            IsPublished = Editor.IsPublished
        };

        await _bonusContentStore.SaveAsync(item, uploadedFile, HttpContext.RequestAborted);
        StatusMessage = "התוכן נשמר בהצלחה.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        await _bonusContentStore.DeleteAsync(id, HttpContext.RequestAborted);
        StatusMessage = "הקטע נמחק.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEditAsync(Guid id)
    {
        await LoadPageAsync(id);
        return Page();
    }

    private async Task LoadPageAsync(Guid? editId)
    {
        Items = await _bonusContentStore.GetAllAsync(HttpContext.RequestAborted);

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
            Summary = item.Summary,
            IsPublished = item.IsPublished,
            ExistingPdfFileName = item.OriginalPdfFileName
        };
    }

    public sealed class EditorInput
    {
        public Guid Id { get; set; }

        [Required]
        [StringLength(120)]
        public string Title { get; set; } = string.Empty;

        [StringLength(280)]
        public string? Summary { get; set; }

        public IFormFile? PdfFile { get; set; }

        public string? ExistingPdfFileName { get; set; }

        public bool IsPublished { get; set; }
    }
}
