namespace Mermaid.DebugVisualizer;

using Microsoft.VisualStudio.Extensibility;

[VisualStudioContribution]
internal class MermaidVisualizerExtension : Extension
{
    public override ExtensionConfiguration ExtensionConfiguration => new()
    {
        Metadata = new(
            "MermaidDebugVisualizer.6f3a2e91-4b7c-4d8e-a5f1-9e2b3c0d1a4f",
            this.ExtensionAssemblyVersion,
            "Community",
            "Mermaid Diagram Debugger Visualizer",
            "Displays Mermaid diagrams during debug sessions for string variables."),
    };
}
