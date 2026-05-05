namespace Mermaid.DebugVisualizer;

using Microsoft.VisualStudio.Extensibility;

[VisualStudioContribution]
internal class MermaidVisualizerExtension : Extension
{
    public override ExtensionConfiguration ExtensionConfiguration => new()
    {
        Metadata = new(
            id: "MermaidDebugVisualizer.6f3a2e91-4b7c-4d8e-a5f1-9e2b3c0d1a4f",
            version: this.ExtensionAssemblyVersion,
            publisherName: "Community",
            displayName: "Mermaid Diagram Debugger Visualizer",
            description: "Displays Mermaid diagrams during debug sessions for string variables."),
    };
}
