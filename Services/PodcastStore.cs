using System.Text.Json;
using BookBoutique.Models;

namespace BookBoutique.Services;

public sealed class PodcastStore
{
    private readonly string _filePath;
    private readonly string _mediaDirectory;
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public PodcastStore(IWebHostEnvironment environment)
    {
        var dataDirectory = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDirectory);
        _filePath = Path.Combine(dataDirectory, "podcasts.json");

        _mediaDirectory = Path.Combine(environment.WebRootPath, "media", "podcasts");
        Directory.CreateDirectory(_mediaDirectory);
    }

    public async Task<IReadOnlyList<PodcastItem>> GetPublishedAsync(CancellationToken cancellationToken)
    {
        var items = await GetAllAsync(cancellationToken);
        return items
            .Where(item => item.IsPublished)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToList();
    }

    public async Task<IReadOnlyList<PodcastItem>> GetAllAsync(CancellationToken cancellationToken)
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

    public async Task SaveAsync(PodcastItem item, IFormFile? audioFile, CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            var items = await ReadUnsafeAsync(cancellationToken);
            var existingIndex = items.FindIndex(existing => existing.Id == item.Id);
            var existing = existingIndex >= 0 ? items[existingIndex] : null;

            if (audioFile is not null)
            {
                if (existing is not null && !string.IsNullOrWhiteSpace(existing.FileName))
                {
                    DeletePhysicalFile(existing.FileName);
                }

                var safeExtension = Path.GetExtension(audioFile.FileName);
                var generatedFileName = $"{Guid.NewGuid():N}{safeExtension}";
                var destinationPath = Path.Combine(_mediaDirectory, generatedFileName);

                await using var stream = File.Create(destinationPath);
                await audioFile.CopyToAsync(stream, cancellationToken);

                item.FileName = generatedFileName;
                item.OriginalFileName = audioFile.FileName;
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

    private async Task<List<PodcastItem>> ReadUnsafeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            await WriteUnsafeAsync([], cancellationToken);
            return [];
        }

        await using var stream = File.OpenRead(_filePath);
        var items = await JsonSerializer.DeserializeAsync<List<PodcastItem>>(stream, cancellationToken: cancellationToken);
        return items ?? [];
    }

    private async Task WriteUnsafeAsync(List<PodcastItem> items, CancellationToken cancellationToken)
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
