using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BookBoutique.Models;
using BookBoutique.Pages;
using QRCoder;

namespace BookBoutique.Services;

public sealed partial class StaticSiteExporter
{
    private const string PublicBaseUrl = "https://ein-hamelech.shakedshira.com";
    private readonly string _contentRoot;
    private readonly string _webRoot;
    private readonly string _outputRoot;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public StaticSiteExporter(string contentRoot, string? outputRoot = null)
    {
        _contentRoot = ResolveContentRoot(contentRoot);
        _webRoot = Path.Combine(_contentRoot, "wwwroot");
        _outputRoot = outputRoot ?? Path.Combine(_contentRoot, "dist");
    }

    public string OutputRoot => _outputRoot;

    public async Task ExportAsync(Uri baseAddress, CancellationToken cancellationToken = default)
    {
        ResetOutputDirectory();

        using var httpClient = new HttpClient
        {
            BaseAddress = baseAddress
        };

        var bonusItems = await ReadJsonAsync<BonusContentItem>("bonus-content.json", cancellationToken);
        var publishedBonusItems = bonusItems
            .Where(item => item.IsPublished)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToList();

        var pages = new List<SnapshotPage>
        {
            new("/", "index.html"),
            new("/FirstChapters", "first-chapters.html"),
            new("/Order", "order.html"),
            new("/OrderDetails", "order-details.html"),
            new("/order-thanks", "order-thanks.html"),
            new("/Bonus", "bonus.html"),
            new("/Podcast", "podcast.html"),
            new("/TheWorld", "the-world.html"),
            new("/Lecture", "lecture.html"),
            new("/UlpanaMap", "ulpana-map.html"),
            new("/Privacy", "privacy.html")
        };

        foreach (var item in publishedBonusItems)
        {
            var slug = BonusChapterModel.CreateSlug(item);
            pages.Add(new($"/Bonus/Chapter/{slug}", $"bonus/{slug}.html"));
        }

        foreach (var page in pages)
        {
            await SnapshotPageAsync(httpClient, page, cancellationToken);
        }

        CopyPublicAssets();
        await WriteBonusQrCodesAsync(publishedBonusItems, cancellationToken);
    }

    private async Task SnapshotPageAsync(HttpClient httpClient, SnapshotPage page, CancellationToken cancellationToken)
    {
        var html = await httpClient.GetStringAsync(page.Route, cancellationToken);
        html = RewriteLinksForStaticOutput(html, GetPrefix(page.OutputPath));

        var outputPath = Path.Combine(_outputRoot, page.OutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(outputPath, html, Encoding.UTF8, cancellationToken);
    }

    private async Task WriteBonusQrCodesAsync(IEnumerable<BonusContentItem> items, CancellationToken cancellationToken)
    {
        var qrDirectory = Path.Combine(_outputRoot, "qr");
        Directory.CreateDirectory(qrDirectory);

        foreach (var item in items)
        {
            var slug = BonusChapterModel.CreateSlug(item);
            var publicUrl = $"{PublicBaseUrl}/bonus/{slug}.html";
            await File.WriteAllTextAsync(
                Path.Combine(qrDirectory, $"{slug}.svg"),
                CreateQrSvg(publicUrl),
                Encoding.UTF8,
                cancellationToken);
        }
    }

    private async Task<IReadOnlyList<T>> ReadJsonAsync<T>(string fileName, CancellationToken cancellationToken)
    {
        var path = Path.Combine(_contentRoot, "App_Data", fileName);
        if (!File.Exists(path))
        {
            return [];
        }

        await using var stream = File.OpenRead(path);
        var items = await JsonSerializer.DeserializeAsync<List<T>>(stream, JsonOptions, cancellationToken);
        return items ?? [];
    }

    private void ResetOutputDirectory()
    {
        if (Directory.Exists(_outputRoot))
        {
            Directory.Delete(_outputRoot, recursive: true);
        }

        Directory.CreateDirectory(_outputRoot);
    }

    private void CopyPublicAssets()
    {
        foreach (var directoryName in new[] { "css", "js", "lib", "media" })
        {
            var source = Path.Combine(_webRoot, directoryName);
            if (!Directory.Exists(source))
            {
                continue;
            }

            CopyDirectory(source, Path.Combine(_outputRoot, directoryName));
        }

        var favicon = Path.Combine(_webRoot, "favicon.ico");
        if (File.Exists(favicon))
        {
            File.Copy(favicon, Path.Combine(_outputRoot, "favicon.ico"), overwrite: true);
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        }

        foreach (var directory in Directory.GetDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }

    private static string RewriteLinksForStaticOutput(string html, string prefix)
    {
        html = ScopedCssLinkRegex().Replace(html, string.Empty);
        html = BonusChapterLinkRegex().Replace(html, match => $"href=\"{prefix}bonus/{match.Groups["slug"].Value}.html\"");
        html = html.Replace("href=\"/\"", $"href=\"{prefix}index.html\"");
        html = html.Replace("href=\"/#preorder\"", $"href=\"{prefix}index.html#preorder\"");
        html = html.Replace("href=\"/FirstChapters\"", $"href=\"{prefix}first-chapters.html\"");
        html = html.Replace("href=\"/Order\"", $"href=\"{prefix}order.html\"");
        html = html.Replace("href=\"/OrderDetails", $"href=\"{prefix}order-details.html");
        html = html.Replace("href=\"/OrderThanks\"", $"href=\"{prefix}order-thanks.html\"");
        html = html.Replace("href=\"/order-thanks\"", $"href=\"{prefix}order-thanks.html\"");
        html = html.Replace("data-thanks-url=\"/OrderThanks\"", $"data-thanks-url=\"{prefix}order-thanks.html\"");
        html = html.Replace("data-thanks-url=\"/order-thanks\"", $"data-thanks-url=\"{prefix}order-thanks.html\"");
        html = html.Replace("href=\"/Bonus\"", $"href=\"{prefix}bonus.html\"");
        html = html.Replace("href=\"/Podcast\"", $"href=\"{prefix}podcast.html\"");
        html = html.Replace("href=\"/TheWorld\"", $"href=\"{prefix}the-world.html\"");
        html = html.Replace("href=\"/Lecture\"", $"href=\"{prefix}lecture.html\"");
        html = html.Replace("href=\"/UlpanaMap\"", $"href=\"{prefix}ulpana-map.html\"");
        html = html.Replace("href=\"/Privacy\"", $"href=\"{prefix}privacy.html\"");
        html = RootAssetLinkRegex().Replace(html, match => $"{match.Groups["attr"].Value}=\"{prefix}{match.Groups["path"].Value}\"");
        html = EncodedRootMediaPathRegex().Replace(html, match => $"&quot;{prefix}{match.Groups["path"].Value}&quot;");
        return html;
    }

    private static string GetPrefix(string outputPath)
    {
        var directory = Path.GetDirectoryName(outputPath);
        return string.IsNullOrWhiteSpace(directory) ? string.Empty : "../";
    }

    private static string CreateQrSvg(string url)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        var qr = new SvgQRCode(data);
        return qr.GetGraphic(8);
    }

    public static string ResolveContentRoot(string candidate)
    {
        var directory = new DirectoryInfo(candidate);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "BookBoutique.csproj")))
            {
                return directory.FullName;
            }

            var childProject = Path.Combine(directory.FullName, "BookBoutique", "BookBoutique.csproj");
            if (File.Exists(childProject))
            {
                return Path.GetDirectoryName(childProject)!;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate BookBoutique.csproj from the current directory.");
    }

    private sealed record SnapshotPage(string Route, string OutputPath);

    [GeneratedRegex("<link[^>]+BookBoutique\\.styles\\.css[^>]*>\\s*", RegexOptions.IgnoreCase)]
    private static partial Regex ScopedCssLinkRegex();

    [GeneratedRegex("href=\"/Bonus/Chapter/(?<slug>chapter-[^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex BonusChapterLinkRegex();

    [GeneratedRegex("(?<attr>href|src|data-pdf-url)=\"/(?<path>(?:css|js|lib|media)/[^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex RootAssetLinkRegex();

    [GeneratedRegex("&quot;/(?<path>media/[^&]+)&quot;", RegexOptions.IgnoreCase)]
    private static partial Regex EncodedRootMediaPathRegex();
}
