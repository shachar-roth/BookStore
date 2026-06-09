using BookBoutique.Models;

namespace BookBoutique.Services;

public sealed class FirstChaptersStore
{
    private const string PdfFileName = "haneara-banikba-first-six-chapters.pdf";
    private const string PageDirectoryName = "haneara-banikba-first-six";
    private readonly string _pdfPath;
    private readonly string _pageImagesRoot;
    private readonly PdfPageImageRenderer _pdfPageImageRenderer;
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public FirstChaptersStore(IWebHostEnvironment environment, PdfPageImageRenderer pdfPageImageRenderer)
    {
        _pdfPageImageRenderer = pdfPageImageRenderer;
        _pdfPath = Path.Combine(environment.WebRootPath, "media", "free-chapters", PdfFileName);
        _pageImagesRoot = Path.Combine(environment.WebRootPath, "media", "free-chapters", "pages");
    }

    public async Task<PdfBookViewerModel> GetBookViewerAsync(CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            var pageUrls = EnsurePageImages(cancellationToken)
                .Select(fileName => $"/media/free-chapters/pages/{fileName}")
                .ToList();

            return new PdfBookViewerModel
            {
                PdfUrl = $"/media/free-chapters/{PdfFileName}",
                PageImageUrls = pageUrls
            };
        }
        finally
        {
            Gate.Release();
        }
    }

    private List<string> EnsurePageImages(CancellationToken cancellationToken)
    {
        var outputDirectory = Path.Combine(_pageImagesRoot, PageDirectoryName);
        if (Directory.Exists(outputDirectory) && Directory.GetFiles(outputDirectory, "page-*.png").Length > 0)
        {
            return Directory.GetFiles(outputDirectory, "page-*.png")
                .OrderBy(Path.GetFileName)
                .Select(file => $"{PageDirectoryName}/{Path.GetFileName(file)}")
                .ToList();
        }

        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }

        return _pdfPageImageRenderer.RenderPdfPageImages(
            _pdfPath,
            outputDirectory,
            PageDirectoryName,
            cancellationToken);
    }
}
