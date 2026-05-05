namespace Mermaid.DebugVisualizer;

using System.Diagnostics;
using System.Runtime.Serialization;

using Microsoft.VisualStudio.Extensibility.UI;

/// <summary>
///     Data context (ViewModel) for the Mermaid visualizer Remote UI control.
///     Renders the Mermaid diagram on construction and exposes it as a file URI for Image binding.
/// </summary>
[DataContract]
internal sealed class MermaidDataContext : NotifyPropertyChangedObject
{
    // Not a DataMember — only used internally for the browser fallback
    private readonly string? svgContent;

    public MermaidDataContext(MermaidContent? content, MermaidRenderService renderService)
    {
        if (content is null)
        {
            this.StatusMessage = "⚠️ No Mermaid content detected in this string.";
            this.MermaidSource = string.Empty;
            this.HasMermaidContent = false;
        }
        else
        {
            this.MermaidSource = content.Source;
            this.HasMermaidContent = true;

            // Render synchronously (Naiad is fast, typically <200ms)
            var (pngPath, svgContentRendered) = renderService.RenderToPng(content.Source);
            if (pngPath is not null)
            {
                this.DiagramImageUri = new Uri(pngPath).AbsoluteUri;
                this.HasRenderedImage = true;
                this.StatusMessage = content.IsEmbeddedInMarkdown
                                             ? "Mermaid diagram (extracted from Markdown)"
                                             : "Mermaid diagram";

                this.svgContent = svgContentRendered;
            }
            else
            {
                this.StatusMessage = "⚠️ Rendering failed — use 'Open in Browser' to view the diagram.";
                this.svgContent = null;
            }
        }

        this.OpenInBrowserCommand = new AsyncCommand(async (_, ct) => await this.OpenInBrowserAsync(ct));
        this.CopySourceCommand = new AsyncCommand(async (_, ct) => await this.CopySourceAsync(ct));
        this.ZoomInCommand = new AsyncCommand((_, _) =>
        {
            this.ZoomLevel = Math.Min(Math.Round(this.ZoomLevel + 0.25, 2), 4.0);
            return Task.CompletedTask;
        });

        this.ZoomOutCommand = new AsyncCommand((_, _) =>
        {
            this.ZoomLevel = Math.Max(Math.Round(this.ZoomLevel - 0.25, 2), 0.25);
            return Task.CompletedTask;
        });

        this.ResetZoomCommand = new AsyncCommand((_, _) =>
        {
            this.ZoomLevel = 1.0;
            return Task.CompletedTask;
        });
    }

    [DataMember]
    public string StatusMessage
    {
        get;
        private set => this.SetProperty(ref field, value);
    }

    [DataMember]
    public string MermaidSource
    {
        get;
        private set => this.SetProperty(ref field, value);
    }

    [DataMember]
    public bool HasMermaidContent
    {
        get;
        private set => this.SetProperty(ref field, value);
    }

    [DataMember]
    public bool HasRenderedImage
    {
        get;
        private set => this.SetProperty(ref field, value);
    }

    /// <summary>
    ///     File URI pointing to the rendered PNG (e.g., "file:///C:/Temp/MermaidVisualizer/abc.png").
    ///     Bound to the XAML Image.Source.
    /// </summary>
    [DataMember]
    public string? DiagramImageUri
    {
        get;
        private set => this.SetProperty(ref field, value);
    }

    [DataMember]
    public IAsyncCommand OpenInBrowserCommand { get; }

    [DataMember]
    public IAsyncCommand CopySourceCommand { get; }

    [DataMember]
    public IAsyncCommand ZoomInCommand { get; }

    [DataMember]
    public IAsyncCommand ZoomOutCommand { get; }

    [DataMember]
    public IAsyncCommand ResetZoomCommand { get; }

    [DataMember]
    public double ZoomLevel
    {
        get;
        private set => this.SetProperty(ref field, value);
    } = 1.0;

    private async Task OpenInBrowserAsync(CancellationToken ct)
    {
        var htmlPath = this.svgContent is not null
                               ? MermaidHtmlGenerator.GenerateWithSvg(this.svgContent, this.MermaidSource)
                               : MermaidHtmlGenerator.GenerateWithCdn(this.MermaidSource);

        var process = Process.Start(new ProcessStartInfo(htmlPath) { UseShellExecute = true });

        if (process is null)
        {
            this.StatusMessage = "⚠️ Failed to open browser.";
            return;
        }

        await process.WaitForExitAsync(ct);
    }

    private async Task CopySourceAsync(CancellationToken ct)
    {
        // Clipboard.SetText() requires STA — use clip.exe for thread safety
        using var proc = new Process();

        proc.StartInfo = new("clip")
        {
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        _ = proc.Start();
        await proc.StandardInput.WriteAsync(this.MermaidSource.AsMemory(), ct);
        proc.StandardInput.Close();
        await proc.WaitForExitAsync(ct);
    }
}
