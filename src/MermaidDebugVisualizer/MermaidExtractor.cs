namespace Mermaid.DebugVisualizer;

using Markdig;
using Markdig.Syntax;

using Naiad;

/// <summary>
///     Detects and extracts Mermaid diagram content from a string.
///     Supports both raw Mermaid syntax and Markdown with fenced ```mermaid blocks.
/// </summary>
internal static class MermaidExtractor
{
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
        return mermaidSource != null ? new(mermaidSource, true) : null;
    }

    // Delegate to Naiad's detector so the set of recognized diagram types never drifts
    // from what the renderer can actually draw.
    private static bool IsRawMermaid(string text)
            => Mermaid.TryDetectDiagramType(text, out _);

    private static string? ExtractFromMarkdown(string markdown)
    {
        var document = Markdown.Parse(markdown, MarkdownPipeline);

        var mermaidBlock = document
                          .Descendants<FencedCodeBlock>()
                          .FirstOrDefault(static b => string.Equals(b.Info, "mermaid", StringComparison.OrdinalIgnoreCase));

        // Extract lines from the code block
        return mermaidBlock?.Lines.ToString().Trim();
    }
}

internal sealed record MermaidContent(string Source, bool IsEmbeddedInMarkdown);
