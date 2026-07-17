# Copilot Instructions

## Build

```powershell
# Build the solution
dotnet build MermaidVisualizer.slnx

# Build a specific project
dotnet build src/Naiad/Naiad.csproj
dotnet build src/MermaidDebugVisualizer/MermaidDebugVisualizer.csproj

# Run the engine tests (xUnit)
dotnet test src/Naiad.Tests/Naiad.Tests.csproj
```

`Naiad.Tests` holds smoke + structural tests covering every diagram type. The
`MermaidDebugVisualizer` extension project targets `net8.0-windows` and can only be
built on Windows (VS extension development workload). CI runs on `windows-latest`
(see `.github/workflows/`).

## Architecture

Two projects in `src/`:

**`Naiad`** — pure C# library that parses Mermaid syntax and renders SVG. Public entry point is `Mermaid.Render(string input, RenderOptions? options)`, which auto-detects the diagram type and dispatches to the matching parser/renderer pair. Each diagram type lives in its own subdirectory under `Diagrams/` with three files: `*Model.cs`, `*Parser.cs`, `*Renderer.cs`.

**`MermaidDebugVisualizer`** — a Visual Studio extension (VS Extensibility SDK) that registers as a debugger visualizer for `string` variables. When triggered:
1. `MermaidVisualizerProvider` reads the string value from the debugger.
2. `MermaidExtractor` detects whether it's raw Mermaid syntax or a Markdown document containing a ` ```mermaid ` fenced block.
3. `MermaidRenderService` calls `Naiad.Mermaid.Render()` → SVG, then converts to PNG via SkiaSharp.
4. `MermaidDataContext` (ViewModel, bound via `[DataMember]`) drives the XAML `RemoteUserControl`.
5. The "Open in Browser" button generates a temp HTML file with the SVG embedded inline; falls back to Mermaid.js CDN if Naiad render failed.

Graph-based diagrams (Flowchart, Class, State, ER, etc.) use a Dagre-inspired layout engine in `Naiad/Layout/` — the pipeline is: Acyclic → Ranker → Ordering → CoordinateAssignment → edge routing.

## Key Conventions

### Adding a new diagram type (the established pattern)

1. Add a `DiagramType` enum value in `Naiad/DiagramType.cs`.
2. Create `src/Naiad/Diagrams/{Name}/` with:
   - `{Name}Model.cs` — record/class inheriting `DiagramBase` (or `GraphDiagramBase` for graph layouts).
   - `{Name}Parser.cs` — `class {Name}Parser : IDiagramParser<{Name}Model>`, built with **Pidgin** parser combinators.
   - `{Name}Renderer.cs` — `class {Name}Renderer : IDiagramRenderer<{Name}Model>`, returns an `SvgDocument`.
3. Add a `global using` for the new namespace in `Naiad/GlobalUsings.cs`.
4. Add type detection in `Mermaid.DetectDiagramType()` (first-line keyword check).
5. Add a `RenderXxx()` private method in `Mermaid.cs` and wire it into the switch.
6. Add the triggering keyword(s) to `Mermaid.DetectFromFirstLine()` (used by both `Render` and the public `TryDetectDiagramType`). The visualizer's `MermaidExtractor` delegates to `TryDetectDiagramType`, so no change is needed there — the two can't drift.

### Parsers (Pidgin)

- All Pidgin primitives (`Token`, `String`, `Try`, `OneOf`, `Char`, etc.) are available globally — no per-file `using static` needed (see `GlobalUsings.cs`).
- Reusable primitives (`Newline`, `LineEnd`, `Comment`, `Identifier`, `QuotedString`, `Number`, `Indentation`, `DirectionParser`) live in `CommonParsers`.
- Always wrap ambiguous alternatives with `Try(...)` to enable backtracking.
- Parser combinator fields in parser classes are `static` (no instance state).
- The canonical pattern: `var result = parser.Parse(input); if (!result.Success) throw new MermaidParseException(...)`.

### SVG rendering

- Use `SvgDocument` + the builder classes in `Naiad/Rendering/` (never raw string concatenation).
- All floating-point values written to SVG must use `CultureInfo.InvariantCulture` (already available as a global using).
- Numbers in SVG attributes use the format string `0.######` (strip trailing zeros).

### VS Extension (Remote UI)

- Extension contribution points are marked `[VisualStudioContribution]`.
- ViewModel properties exposed to XAML bindings must be `[DataMember]` on a class inheriting `NotifyPropertyChangedObject`.
- Use the `field` keyword (C# preview) for auto-property backing fields — `LangVersion = preview` is set in both `.csproj` files.
- Clipboard writes use `clip.exe` via redirected stdin (Remote UI runs in a non-STA context).
- Temp PNG files are kept in `%TEMP%\MermaidVisualizer\`; `MermaidRenderService.CleanupTempFiles()` retains the 20 most recent.
