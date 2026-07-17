# Publishing to the Visual Studio Marketplace

This extension is built with the out-of-process **VisualStudio.Extensibility** SDK.
`dotnet build -c Release` produces a `.vsix`; publishing it to the Marketplace is a
separate step done with `VsixPublisher.exe`.

## One-time setup

1. **Create a Marketplace publisher.** Sign in at
   <https://marketplace.visualstudio.com/manage> and create a publisher. Note its
   **publisher ID** (the slug in the URL, e.g. `tanuscorp`).
2. **Create a Personal Access Token (PAT).** In Azure DevOps
   (<https://dev.azure.com>), create a PAT with the **Marketplace → Manage** scope.
   Keep it secret — store it as the `VS_MARKETPLACE_PAT` GitHub Actions secret for
   the release workflow, and never commit it.
3. **Fill in the publisher ID:**
   - In [`publish-manifest.json`](../publish-manifest.json) → `"publisher"`.
   - Optionally update the display publisher name in
     `src/MermaidDebugVisualizer/MermaidVisualizerExtension.cs`
     (`ExtensionConfiguration.Metadata`).

## Build the VSIX

```powershell
dotnet build MermaidVisualizer.slnx -c Release
# -> src/MermaidDebugVisualizer/bin/Release/net8.0-windows/Mermaid.DebugVisualizer.vsix
```

## Publish

`VsixPublisher.exe` ships with the VS SDK, at:

```
%VSINSTALLDIR%\VSSDK\VisualStudioIntegration\Tools\Bin\VsixPublisher.exe
```

```powershell
VsixPublisher.exe publish `
    -payload  "src/MermaidDebugVisualizer/bin/Release/net8.0-windows/Mermaid.DebugVisualizer.vsix" `
    -publishManifest "publish-manifest.json" `
    -personalAccessToken $env:VS_MARKETPLACE_PAT
```

The GitHub **Release** workflow (`.github/workflows/release.yml`) automates build +
publish. Trigger it either way:

- **Push a `v*` tag** (e.g. `git push origin v0.1.0`), or
- **Actions → Release → Run workflow**, entering the version (e.g. `0.1.0`). The
  workflow then creates the `v0.1.0` tag at the chosen commit itself.

The Marketplace publish step runs only when the `VS_MARKETPLACE_PAT` secret is set;
otherwise it is skipped and only the GitHub Release + `.vsix` artifact are produced.

## Versioning

The extension version comes from the assembly version (`ExtensionAssemblyVersion`).
Set it per release via `<Version>` in `MermaidDebugVisualizer.csproj` (or pass
`-p:Version=x.y.z` to `dotnet build`), tag the commit `vx.y.z`, and update
[`CHANGELOG.md`](../CHANGELOG.md).

## Pre-publish checklist

- [x] `publisher` set in `publish-manifest.json` to your real Marketplace publisher ID (`74nu5`).
- [ ] `VS_MARKETPLACE_PAT` secret configured (for CI) or exported locally.
- [ ] Version bumped and `CHANGELOG.md` updated; commit tagged `vx.y.z`.
- [ ] `dotnet test src/Naiad.Tests/Naiad.Tests.csproj` is green.
- [ ] Icon (`Resources/icon.png`) and preview (`Resources/preview.png`) look correct.
- [ ] `Preview` flag reviewed — the SDK defaults extensions to *preview* on the
      Marketplace; set `Metadata.Preview = false` when you consider it stable.
