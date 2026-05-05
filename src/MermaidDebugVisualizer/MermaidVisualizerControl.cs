namespace MermaidDebugVisualizer;

using Microsoft.VisualStudio.Extensibility.UI;

/// <summary>
/// Remote UI control for the Mermaid debugger visualizer.
/// Displays the rendered diagram as a PNG image and provides action buttons.
/// </summary>
internal sealed class MermaidVisualizerControl : RemoteUserControl
{
    public MermaidVisualizerControl(MermaidContent? content, MermaidRenderService renderService)
        : base(new MermaidDataContext(content, renderService))
    {
    }
}
