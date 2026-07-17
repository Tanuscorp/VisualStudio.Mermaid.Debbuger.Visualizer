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

            // Display publisher name shown in the Extension Manager. The Visual Studio
            // Marketplace *publisher ID* used at publish time is configured separately in
            // the publish manifest (see docs/PUBLISHING.md), not here.
            "Tanuscorp",
            "Mermaid Diagram Debugger Visualizer",
            "Preview Mermaid diagrams from string variables while debugging — flowcharts, "
            + "sequence, class, state, ER, Gantt and 20+ more diagram types rendered inline. "
            + "No browser, no internet, no Node.js required.")
        {
            Icon = @"Resources\icon.png",
            PreviewImage = @"Resources\preview.png",
            License = @"LICENSE.txt",
            MoreInfo = "https://github.com/tanuscorp/VisualStudio.Mermaid.Debbuger.Visualizer",
            ReleaseNotes =
                "https://github.com/tanuscorp/VisualStudio.Mermaid.Debbuger.Visualizer/blob/master/CHANGELOG.md",
            Tags = "mermaid diagram debugger visualizer flowchart sequence class state uml svg debugging",
        },
    };
}
