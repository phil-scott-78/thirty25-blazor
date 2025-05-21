using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.Extensions.Logging;

namespace BlazorStatic.Services.Content.Roslyn;

/// <summary>
/// Handles loading of example assemblies.
/// </summary>
/// <param name="logger"></param>
public class AssemblyLoaderService(ILogger<AssemblyLoaderService> logger)
{
    private readonly ILogger _logger = logger;
    private RoslynAssemblyLoadContext? _loadContext;
    private bool _needsReset;
    private static readonly Dictionary<string, byte[]> AssemblyBytesCache = new();

    internal void ResetContext()
    {
        _needsReset = true;
    }

    private RoslynAssemblyLoadContext GetOrCreateContext()
    {
        if (_needsReset)
        {
            _loadContext?.Unload();
            _loadContext = new RoslynAssemblyLoadContext();
            AssemblyBytesCache.Clear();
        }
        else
        {
            _loadContext ??= new RoslynAssemblyLoadContext();
        }

        return _loadContext;
    }

    internal async Task<Assembly> GetProjectAssembly(Project project, EmitOptions emitOptions)
    {
        var loadContext = GetOrCreateContext();
        var compilation = await project.GetCompilationAsync();
        if (compilation == null)
        {
            throw new Exception($"Could not get compilation for {project.FilePath}");
        }

        var options = compilation.Options
            .WithOptimizationLevel(OptimizationLevel.Release)
            .WithConcurrentBuild(true);

        compilation = compilation.WithOptions(options);

        _logger.LogInformation("--- Compilation Details for Project: {ProjectFilePath} ---", project.FilePath);

        var initialDiagnostics = compilation.GetDiagnostics();
        if (initialDiagnostics.Any())
        {
            _logger.LogInformation("Initial diagnostics from compilation object (before Emit):");
            foreach (var diag in initialDiagnostics.OrderBy(d => d.Severity))
            {
                var logLevel = diag.Severity switch
                {
                    DiagnosticSeverity.Error => LogLevel.Error,
                    DiagnosticSeverity.Warning => LogLevel.Warning,
                    _ => LogLevel.Information
                };
                _logger.Log(logLevel, "  Diagnostic ({Severity}) {Id}: {Message} @ {Location}", diag.Severity, diag.Id, diag.GetMessage(), diag.Location.ToString());
            }
        }
        else
        {
            _logger.LogInformation("No initial diagnostics from compilation object (before Emit).");
        }

        _logger.LogInformation("Compilation references (before Emit):");
        if (compilation.References.Any())
        {
            foreach (var reference in compilation.References)
            {
                string? refPath = (reference as PortableExecutableReference)?.FilePath ?? (reference as CompilationReference)?.Compilation.AssemblyName ?? "N/A";
                _logger.LogInformation("  Reference: Display='{Display}', Path/Name='{Path}'", reference.Display, refPath);
            }
        }
        else
        {
            _logger.LogWarning("No references found in compilation object (before Emit).");
        }
        _logger.LogInformation("--- End Compilation Details ---");

        await using var ms = new MemoryStream();
        var emitResult = compilation.Emit(peStream: ms, options: emitOptions);
        if (!emitResult.Success)
        {
            var failures =
                emitResult.Diagnostics.Where(d => d.IsWarningAsError || d.Severity == DiagnosticSeverity.Error);
            var errorMessage = string.Join(Environment.NewLine, failures.Select(d => $"{d.Id}: {d.GetMessage()}"));
            throw new Exception($"Compilation failed: {errorMessage}");
        }

        foreach (var reference in compilation.References.OfType<PortableExecutableReference>())
        {
            try
            {
                if (reference.FilePath == null || !File.Exists(reference.FilePath)) continue;
                if (reference.FilePath.Contains("Microsoft.NETCore.App.Ref") ||
                    reference.FilePath.Contains(@".Ref\") ||
                    reference.FilePath.Contains(@"\ref\"))
                {
                    // continue;
                }

                var refName = Path.GetFileNameWithoutExtension(reference.FilePath);
                if (AssemblyBytesCache.ContainsKey(refName))
                {
                    continue;
                }

                var refBytes = await File.ReadAllBytesAsync(reference.FilePath);
                AssemblyBytesCache[refName] = refBytes;
                await using var refMs = new MemoryStream(refBytes);
                loadContext.LoadFromStream(refMs);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to preload reference: {FilePath}", reference.FilePath);
            }
        }

        ms.Seek(0, SeekOrigin.Begin);
        var assemblyBytes = ms.ToArray();
        await using var mainMs = new MemoryStream(assemblyBytes);
        var assembly = loadContext.LoadFromStream(mainMs);
        return assembly;
    }
}