namespace MermaidDebugVisualizer;

using System.Diagnostics;
using System.Runtime.InteropServices;

/// <summary>
/// Spawns a MermaidRenderer child process to render Mermaid diagrams.
/// Using a separate process isolates Naiad/SkiaSharp crashes (e.g. StackOverflowException)
/// from the VS extension host, so the visualizer stays alive on rendering failures.
/// </summary>
internal sealed class MermaidRenderService : IDisposable
{
    private static readonly string TempDir = Path.Combine(Path.GetTempPath(), "MermaidVisualizer");
    private static readonly string ExtensionDir =
        Path.GetDirectoryName(typeof(MermaidRenderService).Assembly.Location)!;

    private bool _disposed;

    public MermaidRenderService()
    {
        Directory.CreateDirectory(TempDir);
    }

    /// <summary>
    /// Renders a Mermaid diagram to a PNG file using a child process.
    /// Returns the PNG file path, or null if rendering fails or times out.
    /// </summary>
    public async Task<string?> RenderToPngAsync(string mermaidSource, CancellationToken ct = default)
    {
        var rendererDll = Path.Combine(ExtensionDir, "renderer", "MermaidRenderer.dll");
        if (!File.Exists(rendererDll))
        {
            LogDiagnostic($"Renderer DLL not found: {rendererDll}");
            return null;
        }

        var dotnetExe = FindDotnetExe();
        LogDiagnostic($"Spawning renderer: {dotnetExe} \"{rendererDll}\"");

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(15));

            using var proc = new Process();
            proc.StartInfo = new ProcessStartInfo
            {
                FileName = dotnetExe,
                Arguments = $"\"{rendererDll}\"",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                // Set working dir so .NET finds runtimes/win-x64/native/ relative to the DLL
                WorkingDirectory = Path.GetDirectoryName(rendererDll)!,
            };

            proc.Start();

            await proc.StandardInput.WriteAsync(mermaidSource);
            proc.StandardInput.Close();

            // Read both streams concurrently to avoid deadlock
            var outputTask = proc.StandardOutput.ReadToEndAsync(cts.Token);
            var errorTask = proc.StandardError.ReadToEndAsync(cts.Token);

            try
            {
                await proc.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                LogDiagnostic("Renderer timed out and was killed");
                return null;
            }

            var output = (await outputTask).Trim();
            var error = (await errorTask).Trim();

            if (proc.ExitCode != 0)
            {
                LogDiagnostic($"Renderer exit code {proc.ExitCode}: {error}");
                return null;
            }

            if (!File.Exists(output))
            {
                LogDiagnostic($"Renderer output path not found: {output}");
                return null;
            }

            LogDiagnostic($"Render OK: {output}");
            return output;
        }
        catch (Exception ex)
        {
            LogDiagnostic($"Renderer spawn error: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Removes old PNG files from the temp dir, keeping the 20 most recent.
    /// </summary>
    public static void CleanupTempFiles()
    {
        try
        {
            var stale = Directory.GetFiles(TempDir, "*.png")
                .OrderByDescending(File.GetCreationTime)
                .Skip(20);

            foreach (var f in stale)
                File.Delete(f);
        }
        catch { }
    }

    private static string FindDotnetExe()
    {
        // Prefer DOTNET_ROOT if set (common in CI / VS environments)
        var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrEmpty(dotnetRoot))
        {
            var exe = Path.Combine(dotnetRoot, "dotnet.exe");
            if (File.Exists(exe)) return exe;
        }

        // Derive from the runtime shared directory
        // e.g., C:\Program Files\dotnet\shared\Microsoft.NETCore.App\10.x\ → 3 levels up
        try
        {
            var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
            var candidate = Path.GetFullPath(Path.Combine(runtimeDir, "..", "..", "..", "dotnet.exe"));
            if (File.Exists(candidate)) return candidate;
        }
        catch { }

        return "dotnet"; // fallback — must be on PATH
    }

    private static void LogDiagnostic(string msg)
    {
        try
        {
            File.AppendAllText(
                Path.Combine(TempDir, "diagnostic.log"),
                $"{DateTime.Now:HH:mm:ss.fff} [pid={Environment.ProcessId}] {msg}\n");
        }
        catch { }
    }

    public void Dispose()
    {
        if (!_disposed)
            _disposed = true;
    }
}

