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
    // Static service instance — lightweight, no DI injection needed
    private static readonly MermaidRenderService RenderService = new();

    public override DebuggerVisualizerProviderConfiguration DebuggerVisualizerProviderConfiguration =>
        new("%MermaidDebugVisualizer.MermaidVisualizerProvider.DisplayName%", typeof(string));

    public override async Task<IRemoteUserControl> CreateVisualizerAsync(
        VisualizerTarget visualizerTarget,
        CancellationToken cancellationToken)
    {
        var rawValue = await visualizerTarget.ObjectSource
            .RequestDataAsync<string>(jsonSerializer: null, cancellationToken);

        var mermaidContent = MermaidExtractor.Extract(rawValue);

        MermaidRenderService.CleanupTempFiles();

        return new MermaidVisualizerControl(mermaidContent, RenderService);
    }
}
