namespace Mermaid.DebugVisualizer;

using System.Diagnostics;

using Naiad;

using SkiaSharp;

using Svg.Skia;

/// <summary>
///     Renders Mermaid diagrams using Naiad (Mermaid → SVG) and SkiaSharp (SVG → PNG).
/// </summary>
internal sealed class MermaidRenderService : IDisposable
{
    private static readonly string TempDir = Path.Combine(Path.GetTempPath(), "MermaidVisualizer");
    private bool disposed;

    public MermaidRenderService()
        => Directory.CreateDirectory(TempDir);

    /// <summary>
    ///     Cleans up old PNG files from the temp directory (keeps last 20).
    /// </summary>
    public static void CleanupTempFiles()
    {
        try
        {
            var files = Directory.GetFiles(TempDir, "*.png")
                                 .OrderByDescending(File.GetCreationTime)
                                 .Skip(20);

            foreach (var file in files)
            {
                File.Delete(file);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error rendering cleanup files: {ex}");
            /* best-effort cleanup */
        }
    }

    /// <summary>
    ///     Renders a Mermaid diagram source to an SVG string using Naiad.
    ///     Returns null if rendering fails.
    /// </summary>
    public string? RenderToSvg(string mermaidSource)
    {
        try
        {
            return Mermaid.Render(mermaidSource);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error rendering Mermaid diagram: {ex}");
            return null;
        }
    }

    /// <summary>
    ///     Renders a Mermaid diagram to a PNG file.
    ///     Returns the file path, or null if rendering fails.
    /// </summary>
    public string? RenderToPng(string mermaidSource)
    {
        var svg = this.RenderToSvg(mermaidSource);
        if (svg is null)
        {
            return null;
        }

        try
        {
            return SvgToPng(svg);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error rendering Mermaid diagram to PNG: {ex}");
            return null;
        }
    }

    public void Dispose()
    {
        if (!this.disposed)
        {
            this.disposed = true;
        }
    }

    private static string SvgToPng(string svgContent)
    {
        using var svgDoc = new SKSvg();
        _ = svgDoc.FromSvg(svgContent);

        if (svgDoc.Picture is null)
        {
            throw new InvalidOperationException("Failed to parse SVG.");
        }

        var rect = svgDoc.Picture.CullRect;
        var width = Math.Max((int)rect.Width, 1);
        var height = Math.Max((int)rect.Height, 1);

        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        canvas.DrawPicture(svgDoc.Picture);
        canvas.Flush();

        var pngPath = Path.Combine(TempDir, $"{Guid.NewGuid():N}.png");
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(pngPath, data.ToArray());
        return pngPath;
    }
}
