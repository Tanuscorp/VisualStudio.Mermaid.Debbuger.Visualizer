<div align="center">

<img src="https://raw.githubusercontent.com/Tanuscorp/VisualStudio.Mermaid.Debbuger.Visualizer/master/src/MermaidDebugVisualizer/Resources/icon.png" width="112" alt="Mermaid Diagram Debugger Visualizer" />

# Mermaid Diagram Debugger Visualizer

**Preview [Mermaid](https://mermaid.js.org/) diagrams from string variables while you debug in Visual Studio — rendered inline, with no browser, no internet, and no Node.js.**

</div>

---

## What it does

When you hover a `string` variable in the debugger (or open it from the *Watch* / *Locals* window), Visual Studio shows a magnifying-glass **visualizer** icon. If the string contains a Mermaid diagram — either raw Mermaid syntax or a Markdown document with a ` ```mermaid ` fenced block — this extension renders it as an image right there, so you can *see* the graph your code just built instead of reading its text.

Rendering is done entirely in-process by **[Naiad](#naiad-the-rendering-engine)**, a pure-C# Mermaid engine bundled with the extension. Nothing leaves your machine.

## Features

- 🖼️ **Inline rendering** of Mermaid diagrams during debug sessions for any `string` variable.
- 📄 **Markdown-aware** — automatically extracts the first ` ```mermaid ` block from a Markdown string.
- 🔍 **Zoom** in/out/reset and scroll for large diagrams.
- 🌐 **Open in Browser** — exports a self-contained HTML file with the SVG embedded (no CDN needed); falls back to Mermaid.js only if in-process rendering fails.
- 📋 **Copy source** — copy the raw Mermaid text to the clipboard.
- 🔌 **Offline & self-contained** — no Node.js, no network calls, no external `mmdc` process.

## Supported diagram types

Flowchart · Sequence · Class · State · Entity-Relationship · Gantt · Pie · Git graph · Mindmap · Timeline · User journey · Quadrant · XY chart · Sankey · Block · Kanban · Packet · C4 (Context / Container / Component / Deployment) · Requirement · Architecture · Radar · Treemap

## Requirements

- **Visual Studio 2022** 17.14 or later (the extension uses the out-of-process *VisualStudio.Extensibility* SDK).
- Windows x64 / Arm64.

## Installation

**From the Marketplace** (once published): search for *"Mermaid Diagram Debugger Visualizer"* in **Extensions → Manage Extensions**, or install the `.vsix` from the [Releases](https://github.com/tanuscorp/VisualStudio.Mermaid.Debbuger.Visualizer/releases) page.

**From source** — see [Building](#building-from-source) below.

## Usage

1. Start debugging and break on a line where a `string` holds a Mermaid diagram, e.g.:

   ```csharp
   var diagram = """
       flowchart TD
           A[Start] --> B{Is it working?}
           B -->|Yes| C[Ship it]
           B -->|No| D[Debug] --> B
       """;
   ```

2. In *Locals* / *Watch* / a DataTip, click the magnifying-glass **▾** next to the variable and choose **Mermaid Diagram Visualizer**.
3. The rendered diagram appears. Use the zoom controls, expand **Mermaid source**, **Copy Source**, or **Open in Browser**.

Markdown strings work too — a value containing a ` ```mermaid … ``` ` block is detected automatically.

## How it works

```
string value ──► MermaidExtractor ──► Naiad.Mermaid.Render() ──► SVG ──► SkiaSharp ──► PNG ──► Remote UI (XAML)
                (raw or ```mermaid)     (parse + layout + draw)                                  (zoom / copy / browser)
```

1. `MermaidVisualizerProvider` reads the string value from the debuggee.
2. `MermaidExtractor` decides whether it's raw Mermaid or a Markdown ` ```mermaid ` block — using Naiad's own type detector, so the accepted syntaxes never drift from what can actually be drawn.
3. `MermaidRenderService` calls Naiad to produce an SVG, then rasterizes it to PNG with SkiaSharp.
4. `MermaidDataContext` drives the XAML `RemoteUserControl`.

### Naiad (the rendering engine)

`Naiad` is a standalone, dependency-light C# library (Mermaid → SVG) with a Dagre-inspired layout engine for graph diagrams and [Pidgin](https://github.com/benjamin-hodgson/Pidgin) parser combinators per diagram type. Its single public entry point is:

```csharp
string svg = Naiad.Mermaid.Render(mermaidSource);
```

It has no dependency on Visual Studio and can be reused in any .NET project.

## Building from source

```powershell
# Build everything
dotnet build MermaidVisualizer.slnx -c Release

# Run the engine tests
dotnet test src/Naiad.Tests/Naiad.Tests.csproj

# The .vsix is produced under:
#   src/MermaidDebugVisualizer/bin/Release/net8.0-windows/
```

> The VS extension project targets `net8.0-windows` and requires the **Visual Studio extension development** workload; it can only be built on Windows. The `Naiad` engine and its tests are platform-agnostic.

## Publishing

See **[docs/PUBLISHING.md](docs/PUBLISHING.md)** for the Marketplace publishing checklist (publisher ID, PAT, `VsixPublisher` / release workflow).

## Project layout

| Path | Purpose |
|------|---------|
| `src/Naiad/` | Mermaid → SVG engine (parsers, layout, SVG builders). |
| `src/Naiad.Tests/` | xUnit smoke + structural tests for every diagram type. |
| `src/MermaidDebugVisualizer/` | The Visual Studio debugger-visualizer extension. |
| `.github/workflows/` | CI (build + test) and release (package + publish) pipelines. |

## License

[MIT](LICENSE) © Tanuscorp.

Mermaid is a trademark of its respective owners; this project is an independent renderer and is not affiliated with the Mermaid project.
