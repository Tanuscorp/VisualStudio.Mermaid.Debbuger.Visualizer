# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres
to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Marketplace publishing assets: extension icon, preview image, `README`, `LICENSE`
  (MIT), this changelog, and a publishing guide (`docs/PUBLISHING.md`).
- Marketplace metadata on the extension (`Icon`, `PreviewImage`, `License`,
  `MoreInfo`, `ReleaseNotes`, `Tags`).
- `Naiad.Mermaid.TryDetectDiagramType(...)` — a non-throwing, public diagram-type
  detector that is now the single source of truth for "is this a Mermaid diagram?".
- GitHub Actions: a CI workflow (build + engine tests) and a release workflow that
  packages the `.vsix` on tag.

### Fixed

- The debugger visualizer now recognizes **all** diagram types the engine can render
  in raw form (previously `quadrantChart`, `sankey`, `kanban`, `radar`, `treemap`,
  and `C4Deployment` were only detected inside Markdown blocks). The extractor now
  delegates to Naiad's detector, so the two can no longer drift.
- "Open in Browser" HTML files are now cleaned up alongside the rendered PNGs
  (they previously accumulated indefinitely in the temp directory).

### Changed

- `MermaidRenderService` no longer implements `IDisposable` (its `Dispose` was a
  no-op and the service is a long-lived singleton).

## [0.1.0] - Initial

- Debugger visualizer for `string` variables containing Mermaid diagrams.
- In-process Mermaid → SVG rendering via the Naiad engine, with SkiaSharp
  rasterization to PNG.
- Zoom controls, "Copy Source", and "Open in Browser".
