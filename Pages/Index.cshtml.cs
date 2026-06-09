using System.ComponentModel.DataAnnotations;
using BookBoutique.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookBoutique.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly PreorderStore _preorderStore;

    public IndexModel(ILogger<IndexModel> logger, PreorderStore preorderStore)
    {
        _logger = logger;
        _preorderStore = preorderStore;
    }

    [BindProperty]
    public PreorderInput Input { get; set; } = new();

    [TempData]
    public string? ConfirmationMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        await _preorderStore.SaveAsync(Input, HttpContext.RequestAborted);
        _logger.LogInformation("Preorder request saved for {Email}.", Input.Email);

        ConfirmationMessage = $"{Input.Name}, תודה. בקשת ההזמנה שלך התקבלה.";
        return RedirectToPage();
    }

    public sealed class PreorderInput
    {
        [Required]
        [StringLength(80)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [StringLength(40)]
        public string? City { get; set; }

        [Range(1, 25)]
        public int Quantity { get; set; } = 1;

        [StringLength(500)]
        public string? Message { get; set; }
    }
}
