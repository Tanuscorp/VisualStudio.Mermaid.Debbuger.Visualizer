namespace Mermaid.DebugVisualizer;

using Markdig;
using Markdig.Syntax;

/// <summary>
///     Detects and extracts Mermaid diagram content from a string.
///     Supports both raw Mermaid syntax and Markdown with fenced ```mermaid blocks.
/// </summary>
internal static class MermaidExtractor
{
    // Mermaid diagram type keywords (must appear at the start of a raw diagram)
    private static readonly string[] MermaidKeywords =
    [
        "graph ", "graph\n", "graph\r",
        "flowchart ", "flowchart\n",
        "sequenceDiagram", "classDiagram", "stateDiagram", "stateDiagram-v2",
        "erDiagram", "gantt", "pie ", "pie\n",
        "journey", "gitGraph", "mindmap", "timeline",
        "xychart-beta", "block-beta", "packet-beta", "architecture-beta",
        "requirementDiagram", "C4Context", "C4Container", "C4Component",
    ];

    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder().Build();

    /// <summary>
    ///     Attempts to extract Mermaid content from the given string.
    ///     Returns null if no Mermaid content is found.
    /// </summary>
    public static MermaidContent? Extract(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var trimmed = input.TrimStart();

        // 1. Check for raw Mermaid diagram
        if (IsRawMermaid(trimmed))
            return new(trimmed, false);

        // 2. Check for Markdown with ```mermaid block
        var mermaidSource = ExtractFromMarkdown(input);
        if (mermaidSource != null)
            return new(mermaidSource, true);

        return null;
    }

    private static bool IsRawMermaid(string text) =>
            MermaidKeywords.Any(kw => text.StartsWith(kw, StringComparison.OrdinalIgnoreCase));

    private static string? ExtractFromMarkdown(string markdown)
    {
        var document = Markdown.Parse(markdown, MarkdownPipeline);

        var mermaidBlock = document
                          .Descendants<FencedCodeBlock>()
                          .FirstOrDefault(b => string.Equals(b.Info, "mermaid", StringComparison.OrdinalIgnoreCase));

        if (mermaidBlock is null)
            return null;

        // Extract lines from the code block
        return mermaidBlock.Lines.ToString().Trim();
    }
}

internal sealed record MermaidContent(string Source, bool IsEmbeddedInMarkdown);
