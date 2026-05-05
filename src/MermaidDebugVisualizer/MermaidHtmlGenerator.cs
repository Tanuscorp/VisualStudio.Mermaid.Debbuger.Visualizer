namespace Mermaid.DebugVisualizer;

using System.Security;

/// <summary>
///     Generates a self-contained HTML file with the Mermaid diagram rendered as embedded SVG.
///     No JavaScript or CDN required — the SVG from Naiad is embedded directly.
///     Falls back to Mermaid.js CDN if SVG is not available.
/// </summary>
internal static class MermaidHtmlGenerator
{
    private static readonly string TempDir = Path.Combine(Path.GetTempPath(), "MermaidVisualizer");

    /// <summary>
    ///     Generates an HTML file with the rendered SVG embedded inline.
    ///     Returns the path to the generated file.
    /// </summary>
    public static string GenerateWithSvg(string svgContent, string mermaidSource)
    {
        _ = Directory.CreateDirectory(TempDir);

        // Use raw string without interpolation to avoid CSS brace ambiguity
        var html = """
                   <!DOCTYPE html>
                   <html lang="en">
                   <head>
                     <meta charset="utf-8" />
                     <meta name="viewport" content="width=device-width, initial-scale=1" />
                     <title>Mermaid Diagram</title>
                     <style>
                       * { box-sizing: border-box; margin: 0; padding: 0; }
                       body { background: #1e1e1e; color: #d4d4d4; font-family: Segoe UI, sans-serif; padding: 24px; }
                       h1 { font-size: 1rem; color: #9cdcfe; margin-bottom: 16px; }
                       .diagram { background: #fff; border-radius: 8px; padding: 24px; display: inline-block; max-width: 100%; }
                       .diagram svg { max-width: 100%; height: auto; }
                       details { margin-top: 20px; }
                       summary { cursor: pointer; color: #9cdcfe; font-size: 0.85rem; }
                       pre { background: #252526; padding: 12px; border-radius: 4px; margin-top: 8px;
                             font-family: Consolas, monospace; font-size: 0.8rem; overflow: auto;
                             color: #ce9178; white-space: pre-wrap; }
                     </style>
                   </head>
                   <body>
                     <h1>Mermaid Diagram Visualizer</h1>
                     <div class="diagram">%%SVG%%</div>
                     <details>
                       <summary>Source code</summary>
                       <pre>%%SOURCE%%</pre>
                     </details>
                   </body>
                   </html>
                   """
                  .Replace("%%SVG%%", svgContent)
                  .Replace("%%SOURCE%%", SecurityElement.Escape(mermaidSource));

        var path = Path.Combine(TempDir, $"diagram_{Guid.NewGuid():N}.html");
        File.WriteAllText(path, html);
        return path;
    }

    /// <summary>
    ///     Generates an HTML file using Mermaid.js CDN for rendering.
    ///     Used as a fallback when Naiad cannot render the diagram.
    /// </summary>
    public static string GenerateWithCdn(string mermaidSource)
    {
        _ = Directory.CreateDirectory(TempDir);

        // Use raw string without interpolation to avoid CSS brace ambiguity
        var html = """
                   <!DOCTYPE html>
                   <html lang="en">
                   <head>
                     <meta charset="utf-8" />
                     <title>Mermaid Diagram</title>
                     <style>
                       body { background: #1e1e1e; color: #d4d4d4; font-family: Segoe UI, sans-serif; padding: 24px; }
                       .mermaid { background: #fff; border-radius: 8px; padding: 24px; display: inline-block; }
                     </style>
                   </head>
                   <body>
                     <pre class="mermaid">%%SOURCE%%</pre>
                     <script src="https://cdn.jsdelivr.net/npm/mermaid/dist/mermaid.min.js"></script>
                     <script>mermaid.initialize({ startOnLoad: true, theme: 'default' });</script>
                   </body>
                   </html>
                   """
               .Replace("%%SOURCE%%", SecurityElement.Escape(mermaidSource));

        var path = Path.Combine(TempDir, $"diagram_{Guid.NewGuid():N}.html");
        File.WriteAllText(path, html);
        return path;
    }
}
