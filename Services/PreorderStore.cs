using System.Text.Json;
using BookBoutique.Pages;

namespace BookBoutique.Services;

public sealed class PreorderStore
{
    private readonly string _filePath;
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public PreorderStore(IWebHostEnvironment environment)
    {
        var dataDirectory = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDirectory);
        _filePath = Path.Combine(dataDirectory, "preorders.jsonl");
    }

    public async Task SaveAsync(IndexModel.PreorderInput input, CancellationToken cancellationToken)
    {
        var record = new
        {
            input.Name,
            input.Email,
            input.City,
            input.Quantity,
            input.Message,
            SubmittedAtUtc = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(record);

        await Gate.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(_filePath, json + Environment.NewLine, cancellationToken);
        }
        finally
        {
            Gate.Release();
        }
    }
}
