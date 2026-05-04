namespace MermaidDebugVisualizer;

using System.Diagnostics;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility.UI;

/// <summary>
/// Data context (ViewModel) for the Mermaid visualizer Remote UI control.
/// Receives a pre-rendered PNG path from the provider (rendered in an isolated child process).
/// </summary>
[DataContract]
internal sealed class MermaidDataContext : NotifyPropertyChangedObject
{
    private string _statusMessage = string.Empty;
    private string _mermaidSource = string.Empty;
    private bool _hasMermaidContent;
    private bool _hasRenderedImage;
    private string? _diagramImageUri;

    public MermaidDataContext(MermaidContent? content, string? pngPath)
    {
        if (content is null)
        {
            StatusMessage = "⚠️ No Mermaid content detected in this string.";
            MermaidSource = string.Empty;
            HasMermaidContent = false;
        }
        else
        {
            MermaidSource = content.Source;
            HasMermaidContent = true;

            if (pngPath is not null)
            {
                DiagramImageUri = new Uri(pngPath).AbsoluteUri;
                HasRenderedImage = true;
                StatusMessage = content.IsEmbeddedInMarkdown
                    ? "Mermaid diagram (extracted from Markdown)"
                    : "Mermaid diagram";
            }
            else
            {
                StatusMessage = "⚠️ Rendering failed — use 'Open in Browser' to view the diagram.";
            }
        }

        OpenInBrowserCommand = new AsyncCommand(async (_, ct) => await OpenInBrowserAsync(ct));
        CopySourceCommand = new AsyncCommand(async (_, ct) => await CopySourceAsync(ct));
    }

    [DataMember]
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    [DataMember]
    public string MermaidSource
    {
        get => _mermaidSource;
        private set => SetProperty(ref _mermaidSource, value);
    }

    [DataMember]
    public bool HasMermaidContent
    {
        get => _hasMermaidContent;
        private set => SetProperty(ref _hasMermaidContent, value);
    }

    [DataMember]
    public bool HasRenderedImage
    {
        get => _hasRenderedImage;
        private set => SetProperty(ref _hasRenderedImage, value);
    }

    /// <summary>
    /// File URI pointing to the rendered PNG (e.g., "file:///C:/Temp/MermaidVisualizer/abc.png").
    /// Bound to the XAML Image.Source — WPF ImageSourceConverter handles string→BitmapImage.
    /// </summary>
    [DataMember]
    public string? DiagramImageUri
    {
        get => _diagramImageUri;
        private set => SetProperty(ref _diagramImageUri, value);
    }

    [DataMember]
    public IAsyncCommand OpenInBrowserCommand { get; }

    [DataMember]
    public IAsyncCommand CopySourceCommand { get; }

    private Task OpenInBrowserAsync(CancellationToken ct)
    {
        // Always use Mermaid.js CDN for browser rendering — handles Unicode and all diagram types
        var htmlPath = MermaidHtmlGenerator.GenerateWithCdn(MermaidSource);
        Process.Start(new ProcessStartInfo(htmlPath) { UseShellExecute = true });
        return Task.CompletedTask;
    }

    private async Task CopySourceAsync(CancellationToken ct)
    {
        // Clipboard.SetText() requires STA thread — use clip.exe as a workaround
        using var proc = new Process
        {
            StartInfo = new ProcessStartInfo("clip")
            {
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
        proc.Start();
        await proc.StandardInput.WriteAsync(MermaidSource);
        proc.StandardInput.Close();
        await proc.WaitForExitAsync(ct);
    }
}
