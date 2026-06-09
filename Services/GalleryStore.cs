using System.Text.Json;
using BookBoutique.Models;

namespace BookBoutique.Services;

public sealed class GalleryStore
{
    private readonly string _filePath;
    private readonly string _mediaDirectory;
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public GalleryStore(IWebHostEnvironment environment)
    {
        var dataDirectory = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDirectory);
        _filePath = Path.Combine(dataDirectory, "gallery-images.json");

        _mediaDirectory = Path.Combine(environment.WebRootPath, "media", "gallery");
        Directory.CreateDirectory(_mediaDirectory);
    }

    public async Task<IReadOnlyList<GalleryImageItem>> GetPublishedAsync(CancellationToken cancellationToken)
    {
        var items = await GetAllAsync(cancellationToken);
        return items
            .Where(item => item.IsPublished)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToList();
    }

    public async Task<IReadOnlyList<GalleryImageItem>> GetAllAsync(CancellationToken cancellationToken)
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

    public async Task SaveAsync(GalleryImageItem item, IFormFile? imageFile, CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            var items = await ReadUnsafeAsync(cancellationToken);
            var existingIndex = items.FindIndex(existing => existing.Id == item.Id);
            var existing = existingIndex >= 0 ? items[existingIndex] : null;

            if (imageFile is not null)
            {
                if (existing is not null && !string.IsNullOrWhiteSpace(existing.FileName))
                {
                    DeletePhysicalFile(existing.FileName);
                }

                var safeExtension = Path.GetExtension(imageFile.FileName);
                var generatedFileName = $"{Guid.NewGuid():N}{safeExtension}";
                var destinationPath = Path.Combine(_mediaDirectory, generatedFileName);

                await using var stream = File.Create(destinationPath);
                await imageFile.CopyToAsync(stream, cancellationToken);

                item.FileName = generatedFileName;
                item.OriginalFileName = imageFile.FileName;
            }
            else if (existing is not null)
            {
                item.FileName = existing.FileName;
                item.OriginalFileName = existing.OriginalFileName;
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
            DeletePhysicalFile(existing.FileName);
            await WriteUnsafeAsync(items, cancellationToken);
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task<List<GalleryImageItem>> ReadUnsafeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            await WriteUnsafeAsync([], cancellationToken);
            return [];
        }

        await using var stream = File.OpenRead(_filePath);
        var items = await JsonSerializer.DeserializeAsync<List<GalleryImageItem>>(stream, cancellationToken: cancellationToken);
        return items ?? [];
    }

    private async Task WriteUnsafeAsync(List<GalleryImageItem> items, CancellationToken cancellationToken)
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
}
