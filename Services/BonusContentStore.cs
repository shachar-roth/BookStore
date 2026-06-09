using System.Text.Json;
using BookBoutique.Models;

namespace BookBoutique.Services;

public sealed class BonusContentStore
{
    private readonly string _filePath;
    private readonly string _mediaDirectory;
    private readonly string _pageImagesDirectory;
    private readonly PdfPageImageRenderer _pdfPageImageRenderer;
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public BonusContentStore(IWebHostEnvironment environment, PdfPageImageRenderer pdfPageImageRenderer)
    {
        _pdfPageImageRenderer = pdfPageImageRenderer;
        var dataDirectory = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDirectory);
        _filePath = Path.Combine(dataDirectory, "bonus-content.json");

        _mediaDirectory = Path.Combine(environment.WebRootPath, "media", "bonus-pdfs");
        Directory.CreateDirectory(_mediaDirectory);

        _pageImagesDirectory = Path.Combine(environment.WebRootPath, "media", "bonus-pages");
        Directory.CreateDirectory(_pageImagesDirectory);
    }

    public async Task<IReadOnlyList<BonusContentItem>> GetPublishedAsync(CancellationToken cancellationToken)
    {
        var items = await GetAllAsync(cancellationToken);
        return items
            .Where(item => item.IsPublished)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToList();
    }

    public async Task<IReadOnlyList<BonusContentItem>> GetAllAsync(CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            var items = await ReadUnsafeAsync(cancellationToken);
            return items
                .OrderByDescending(item => item.UpdatedAtUtc)
                .ToList();
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task SaveAsync(BonusContentItem item, IFormFile? pdfFile, CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            var items = await ReadUnsafeAsync(cancellationToken);
            var existingIndex = items.FindIndex(existing => existing.Id == item.Id);
            var existing = existingIndex >= 0 ? items[existingIndex] : null;

            if (pdfFile is not null)
            {
                if (existing is not null && !string.IsNullOrWhiteSpace(existing.PdfFileName))
                {
                    DeletePhysicalFile(existing.PdfFileName);
                    DeletePageImages(existing);
                }

                var generatedFileName = $"{Guid.NewGuid():N}.pdf";
                var destinationPath = Path.Combine(_mediaDirectory, generatedFileName);

                await using (var stream = File.Create(destinationPath))
                {
                    await pdfFile.CopyToAsync(stream, cancellationToken);
                }

                item.PdfFileName = generatedFileName;
                item.OriginalPdfFileName = pdfFile.FileName;
                item.PdfPageImageFileNames = RenderPdfPageImages(item, cancellationToken);
            }
            else if (existing is not null)
            {
                item.PdfFileName = existing.PdfFileName;
                item.OriginalPdfFileName = existing.OriginalPdfFileName;
                item.PdfPageImageFileNames = existing.PdfPageImageFileNames;
            }

            item.UpdatedAtUtc = DateTime.UtcNow;

            if (existingIndex >= 0)
            {
                items[existingIndex] = item;
            }
            else
            {
                items.Add(item);
            }

            await WriteUnsafeAsync(items, cancellationToken);
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            var items = await ReadUnsafeAsync(cancellationToken);
            var existing = items.FirstOrDefault(item => item.Id == id);
            if (existing is null)
            {
                return;
            }

            items.RemoveAll(item => item.Id == id);
            DeletePhysicalFile(existing.PdfFileName);
            DeletePageImages(existing);
            await WriteUnsafeAsync(items, cancellationToken);
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<BonusContentItem?> EnsurePageImagesAsync(Guid id, CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            var items = await ReadUnsafeAsync(cancellationToken);
            var existing = items.FirstOrDefault(item => item.Id == id);
            if (existing is null)
            {
                return null;
            }

            if (HasUsablePageImages(existing))
            {
                return existing;
            }

            DeletePageImages(existing);
            existing.PdfPageImageFileNames = RenderPdfPageImages(existing, cancellationToken);
            await WriteUnsafeAsync(items, cancellationToken);
            return existing;
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task<List<BonusContentItem>> ReadUnsafeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            var starterContent = new List<BonusContentItem>
            {
                new()
                {
                    Title = "פתיח בלעדי לקוראי האתר",
                    Summary = "כאן תוכלו לשתף קטעים שנערכו החוצה, סצנות נוספות או פרקים מיוחדים.",
                    Body = "זהו תוכן התחלתי לדוגמה. החליפו אותו בפרקים האמיתיים שתרצו לפרסם באתר.",
                    IsPublished = true
                }
            };

            await WriteUnsafeAsync(starterContent, cancellationToken);
            return starterContent;
        }

        await using var stream = File.OpenRead(_filePath);
        var items = await JsonSerializer.DeserializeAsync<List<BonusContentItem>>(stream, cancellationToken: cancellationToken);
        return items ?? new List<BonusContentItem>();
    }

    private async Task WriteUnsafeAsync(List<BonusContentItem> items, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, items, JsonOptions, cancellationToken);
    }

    private void DeletePhysicalFile(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        var fullPath = Path.Combine(_mediaDirectory, fileName);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    private bool HasUsablePageImages(BonusContentItem item)
    {
        return item.PdfPageImageFileNames.Count > 0
            && item.PdfPageImageFileNames.All(fileName => File.Exists(Path.Combine(_pageImagesDirectory, fileName)));
    }

    private List<string> RenderPdfPageImages(BonusContentItem item, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(item.PdfFileName))
        {
            return [];
        }

        var pdfPath = Path.Combine(_mediaDirectory, item.PdfFileName);
        if (!File.Exists(pdfPath))
        {
            return [];
        }

        var itemDirectoryName = item.Id.ToString("N");
        var itemDirectory = Path.Combine(_pageImagesDirectory, itemDirectoryName);
        Directory.CreateDirectory(itemDirectory);

        return _pdfPageImageRenderer.RenderPdfPageImages(
            pdfPath,
            itemDirectory,
            itemDirectoryName,
            cancellationToken);
    }

    private void DeletePageImages(BonusContentItem item)
    {
        var itemDirectory = Path.Combine(_pageImagesDirectory, item.Id.ToString("N"));
        if (Directory.Exists(itemDirectory))
        {
            Directory.Delete(itemDirectory, recursive: true);
        }
    }
}
