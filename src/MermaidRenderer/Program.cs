using MermaidSharp;
using SkiaSharp;
using Svg.Skia;

// Run in a dedicated thread with a 64 MB stack to avoid StackOverflowException
// from Pidgin's recursive parser on complex Mermaid diagrams.
int exitCode = 1;
var renderThread = new Thread(() => exitCode = RunRenderer().GetAwaiter().GetResult(), 64 * 1024 * 1024);
renderThread.Start();
renderThread.Join();
return exitCode;

static async Task<int> RunRenderer()
{
    var source = await Console.In.ReadToEndAsync();

    if (string.IsNullOrWhiteSpace(source))
    {
        Console.Error.WriteLine("Empty input");
        return 1;
    }

    try
    {
        var svg = Mermaid.Render(source);
        var pngPath = RenderToPng(svg);
        Console.Write(pngPath);
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Rendering failed: {ex.GetType().Name}: {ex.Message}");
        return 1;
    }
}

static string RenderToPng(string svgContent)
{
    using var svgDoc = new SKSvg();
    svgDoc.FromSvg(svgContent);

    if (svgDoc.Picture is null)
        throw new InvalidOperationException("Failed to parse SVG.");

    var rect = svgDoc.Picture.CullRect;
    int width = Math.Max((int)rect.Width, 1);
    int height = Math.Max((int)rect.Height, 1);

    using var bitmap = new SKBitmap(width, height);
    using var canvas = new SKCanvas(bitmap);
    canvas.Clear(SKColors.White);
    canvas.DrawPicture(svgDoc.Picture);
    canvas.Flush();

    var tempDir = Path.Combine(Path.GetTempPath(), "MermaidVisualizer");
    Directory.CreateDirectory(tempDir);

    var pngPath = Path.Combine(tempDir, $"{Guid.NewGuid():N}.png");
    using var image = SKImage.FromBitmap(bitmap);
    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
    File.WriteAllBytes(pngPath, data.ToArray());
    return pngPath;
}
