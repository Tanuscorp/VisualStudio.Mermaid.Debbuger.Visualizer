namespace MermaidDebugVisualizer;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.DebuggerVisualizers;
using Microsoft.VisualStudio.RpcContracts.RemoteUI;

/// <summary>
/// Debugger visualizer provider for <see cref="string"/> variables containing Mermaid diagrams.
/// </summary>
[VisualStudioContribution]
internal sealed class MermaidVisualizerProvider : DebuggerVisualizerProvider
{
    private static readonly MermaidRenderService RenderService = new();

    public MermaidVisualizerProvider(MermaidVisualizerExtension extension, VisualStudioExtensibility extensibility)
        : base(extension, extensibility)
    {
    }

    public override DebuggerVisualizerProviderConfiguration DebuggerVisualizerProviderConfiguration =>
        new("%MermaidDebugVisualizer.MermaidVisualizerProvider.DisplayName%", typeof(string));

    public override async Task<IRemoteUserControl> CreateVisualizerAsync(
        VisualizerTarget visualizerTarget,
        CancellationToken cancellationToken)
    {
        MermaidRenderService.CleanupTempFiles();

        var rawValue = await visualizerTarget.ObjectSource
            .RequestDataAsync<string>(jsonSerializer: null, cancellationToken);

        var mermaidContent = MermaidExtractor.Extract(rawValue);

        // Render to PNG in child process — crash-safe (SOE in renderer won't kill extension host)
        string? pngPath = null;
        if (mermaidContent is not null)
            pngPath = await RenderService.RenderToPngAsync(mermaidContent.Source, cancellationToken);

        return new MermaidVisualizerControl(mermaidContent, pngPath);
    }
}
