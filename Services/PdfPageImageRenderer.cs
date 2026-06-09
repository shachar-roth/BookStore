using Docnet.Core;
using Docnet.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace BookBoutique.Services;

public sealed class PdfPageImageRenderer
{
    public List<string> RenderPdfPageImages(
        string pdfPath,
        string outputDirectory,
        string publicDirectoryName,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(pdfPath))
        {
            return [];
        }

        Directory.CreateDirectory(outputDirectory);

        using var docReader = DocLib.Instance.GetDocReader(pdfPath, new PageDimensions(2.4));
        var pageCount = docReader.GetPageCount();
        var imageFileNames = new List<string>(pageCount);
        var encoder = new PngEncoder();

        for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var pageReader = docReader.GetPageReader(pageIndex);
            var width = pageReader.GetPageWidth();
            var height = pageReader.GetPageHeight();
            var rawBytes = pageReader.GetImage();
            CompositeBgraOverWhite(rawBytes);

            using var image = Image.LoadPixelData<Bgra32>(rawBytes, width, height);
            var fileName = $"page-{pageIndex + 1:D3}.png";
            image.Save(Path.Combine(outputDirectory, fileName), encoder);

            imageFileNames.Add($"{publicDirectoryName}/{fileName}");
        }

        return imageFileNames;
    }

    private static void CompositeBgraOverWhite(byte[] rawBytes)
    {
        for (var index = 0; index < rawBytes.Length; index += 4)
        {
            var alpha = rawBytes[index + 3];
            if (alpha == 255)
            {
                continue;
            }

            if (alpha == 0)
            {
                rawBytes[index] = 255;
                rawBytes[index + 1] = 255;
                rawBytes[index + 2] = 255;
                rawBytes[index + 3] = 255;
                continue;
            }

            rawBytes[index] = CompositeChannelOverWhite(rawBytes[index], alpha);
            rawBytes[index + 1] = CompositeChannelOverWhite(rawBytes[index + 1], alpha);
            rawBytes[index + 2] = CompositeChannelOverWhite(rawBytes[index + 2], alpha);
            rawBytes[index + 3] = 255;
        }
    }

    private static byte CompositeChannelOverWhite(byte channel, byte alpha) =>
        (byte)((channel * alpha + 255 * (255 - alpha)) / 255);
}
