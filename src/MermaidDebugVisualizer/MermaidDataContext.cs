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
            var (pngPath, svgContent) = renderService.RenderToPng(content.Source);
            if (pngPath is not null)
            {
                this.DiagramImageUri = new Uri(pngPath).AbsoluteUri;
                this.HasRenderedImage = true;
                this.StatusMessage = content.IsEmbeddedInMarkdown
                                             ? "Mermaid diagram (extracted from Markdown)"
                                             : "Mermaid diagram";

                this.svgContent = svgContent;
            }
            else
            {
                this.StatusMessage = "⚠️ Rendering failed — use 'Open in Browser' to view the diagram.";
                this.svgContent = null;
            }
        }

        this.OpenInBrowserCommand = new AsyncCommand(async (_, ct) => await this.OpenInBrowserAsync(ct));
        this.CopySourceCommand = new AsyncCommand(async (_, ct) => await this.CopySourceAsync(ct));
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

    private Task OpenInBrowserAsync(CancellationToken ct)
    {
        var htmlPath = this.svgContent is not null
                               ? MermaidHtmlGenerator.GenerateWithSvg(this.svgContent, this.MermaidSource)
                               : MermaidHtmlGenerator.GenerateWithCdn(this.MermaidSource);

        Process.Start(new ProcessStartInfo(htmlPath)
        {
            UseShellExecute = true,
        });

        return Task.CompletedTask;
    }

    private async Task CopySourceAsync(CancellationToken ct)
    {
        // Clipboard.SetText() requires STA — use clip.exe for thread safety
        using var proc = new Process
        {
            StartInfo = new("clip")
            {
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        proc.Start();
        await proc.StandardInput.WriteAsync(this.MermaidSource);
        proc.StandardInput.Close();
        await proc.WaitForExitAsync(ct);
    }
}
