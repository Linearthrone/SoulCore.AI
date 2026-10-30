using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using SoulCore.Inference.Tools.FS;

namespace SoulCore.Inference.Tools.SelfRepair;

/// <summary>
/// Reads and replaces Victoria's tool source, then compiles that source and runs
/// a snippet in a child process. The running host keeps the assemblies it loaded
/// at startup until it is rebuilt and restarted.
/// </summary>
public sealed partial class ToolSourceStore
{
    public const int MaxCodeChars = 512_000;
    private static readonly TimeSpan CompileTimeout = TimeSpan.FromMinutes(8);
    private static readonly TimeSpan SnippetTimeout = TimeSpan.FromSeconds(15);
    private static readonly SemaphoreSlim BuildLock = new(1, 1);
    private static readonly string GraphRoot = Path.Combine(Path.GetTempPath(), "victoria-tool-sandbox", "graph");
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private readonly string? _toolsDir;
    private readonly string? _inferenceProject;
    private readonly IToolProcessRunner _runner;

    public ToolSourceStore(string? toolsDirectory, string? inferenceProjectPath, IToolProcessRunner? runner = null)
    {
        _toolsDir = string.IsNullOrWhiteSpace(toolsDirectory) ? null : FinalDirectory(toolsDirectory);
        _inferenceProject = string.IsNullOrWhiteSpace(inferenceProjectPath)
            ? null
            : Path.GetFullPath(inferenceProjectPath);
        _runner = runner ?? new ToolProcessRunner();
    }

    public static ToolSourceStore CreateDefault([CallerFilePath] string callerFile = "")
    {
        var package = FilesystemGuard.FindSoulCorePackageRoot() ?? FindPackageFromCaller(callerFile);
        if (package is null)
            return new ToolSourceStore(null, null);

        var tools = Path.Combine(package, "SoulCore.Inference", "Tools");
        var project = Path.Combine(package, "SoulCore.Inference", "SoulCore.Inference.csproj");
        return new ToolSourceStore(
            Directory.Exists(tools) ? tools : null,
            File.Exists(project) ? project : null);
    }

    public ToolResult Read(string? toolName)
    {
        if (!TryFindToolFile(toolName, out var path, out var error))
            return new ToolResult(false, error, null);

        try
        {
            var content = File.ReadAllText(path);
            if (content.Length > MaxCodeChars)
            {
                return new ToolResult(
                    false,
                    $"Tool file is {content.Length} chars, over the {MaxCodeChars} limit. Refusing to return a partial file.",
                    null);
            }

            return new ToolResult(true, content, new { path });
        }
        catch (Exception ex)
        {
            return new ToolResult(false, $"Failed to read tool: {ex.Message}", null);
        }
    }

    public ToolResult Write(string? toolName, string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return new ToolResult(false, "code is empty", null);
        if (code.Length > MaxCodeChars)
            return new ToolResult(false, $"code is {code.Length} chars, over the {MaxCodeChars} limit.", null);
        if (!TryFindToolFile(toolName, out var path, out var error))
            return new ToolResult(false, error, null);

        var backupPath = path + "." + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".bak";
        try
        {
            File.Copy(path, backupPath, overwrite: false);
            File.WriteAllText(path, code);
            return new ToolResult(
                true,
                $"Successfully updated {toolName}. Backup created at {backupPath}. The running host still has the previous assembly until it is rebuilt and restarted.",
                new { path, backupPath });
        }
        catch (Exception ex)
        {
            return new ToolResult(false, $"Failed to write tool: {ex.Message}", null);
        }
    }

    public ToolResult Reload()
    {
        if (_toolsDir is null || !Directory.Exists(_toolsDir))
            return new ToolResult(false, "Reload failed: tool directory was not found.", null);

        var count = Directory.EnumerateFiles(_toolsDir, "*.cs", SearchOption.AllDirectories)
            .Count(p => IsUnderTools(p) && !IsBuildOutput(p));
        return new ToolResult(
            true,
            $"Tool source is on disk ({count} .cs files under {_toolsDir}). The running host still uses the assemblies loaded at startup. A write_tool_code edit is not live until SoulCore.Host is rebuilt and restarted. Use run_test_sandbox first; that compiles the edited source in a separate process.",
            new { toolsDirectory = _toolsDir, fileCount = count });
    }

    public async Task<ToolResult> RunTestSandboxAsync(string? testCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(testCode))
            return new ToolResult(false, "test_code is empty", null);
        if (testCode.Length > MaxCodeChars)
            return new ToolResult(false, $"test_code is {testCode.Length} chars, over the {MaxCodeChars} limit.", null);
        if (_inferenceProject is null || !File.Exists(_inferenceProject))
            return new ToolResult(false, "Sandbox Error: SoulCore.Inference project was not found, so the fix cannot be compiled.", null);

        testCode = StripFences(testCode);
        var runDir = Path.Combine(Path.GetTempPath(), "victoria-tool-sandbox", "runs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runDir);
        Directory.CreateDirectory(GraphRoot);
        try
        {
            await BuildLock.WaitAsync(ct).ConfigureAwait(false);
            ToolProcessResult build;
            var libDir = Path.Combine(runDir, "lib");
            try
            {
                build = await _runner.RunAsync(
                    "dotnet",
                    new[]
                    {
                        "build",
                        _inferenceProject,
                        "-c", "Debug",
                        "--nologo",
                        "-v", "q",
                        "-p:VictoriaToolSandbox=true",
                        $"-p:VictoriaToolSandboxRoot={GraphRoot}",
                    },
                    runDir,
                    CompileTimeout,
                    ct).ConfigureAwait(false);

                if (build.ExitCode == 0 && !build.TimedOut && !CopyBuildOutputs(GraphRoot, libDir))
                {
                    return new ToolResult(
                        false,
                        "Sandbox Error: compile finished but no assemblies were produced.\n" + FormatOutput(build),
                        null);
                }
            }
            finally
            {
                BuildLock.Release();
            }

            if (build.TimedOut)
                return new ToolResult(false, "Error: Test compile timed out.", null);
            if (build.ExitCode != 0)
                return new ToolResult(false, FormatOutput(build), null);

            var appDir = Path.Combine(runDir, "app");
            Directory.CreateDirectory(appDir);
            var projectPath = Path.Combine(runDir, "VictoriaSandbox.csproj");
            File.WriteAllText(projectPath, BuildSnippetProject(libDir));
            File.WriteAllText(Path.Combine(runDir, "Program.cs"), testCode);

            var snippetBuild = await _runner.RunAsync(
                "dotnet",
                new[]
                {
                    "build",
                    projectPath,
                    "-c", "Debug",
                    "--nologo",
                    "-v", "q",
                    "-o", appDir,
                    "-p:RunAnalyzers=false",
                    "-p:RunAnalyzersDuringBuild=false",
                },
                runDir,
                CompileTimeout,
                ct).ConfigureAwait(false);

            if (snippetBuild.TimedOut)
                return new ToolResult(false, "Error: Test compile timed out.", null);
            if (snippetBuild.ExitCode != 0)
                return new ToolResult(false, FormatOutput(snippetBuild), null);

            var dll = Path.Combine(appDir, "VictoriaSandbox.dll");
            if (!File.Exists(dll))
                return new ToolResult(false, "Sandbox Error: snippet compiled but VictoriaSandbox.dll was not produced.", null);

            var run = await _runner.RunAsync(
                "dotnet",
                new[] { dll },
                runDir,
                SnippetTimeout,
                ct,
                scrubSecrets: true).ConfigureAwait(false);

            if (run.TimedOut)
                return new ToolResult(false, "Error: Test timed out (possible infinite loop created).", null);

            if (run.ExitCode != 0)
                return new ToolResult(false, FormatOutput(run), null);
            if (string.IsNullOrWhiteSpace(run.Stdout) && string.IsNullOrWhiteSpace(run.Stderr))
                return new ToolResult(true, "Test executed successfully (no output).", null);
            return new ToolResult(true, FormatOutput(run), null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ToolResult(false, $"Sandbox Error: {ex.Message}", null);
        }
        finally
        {
            await TryDeleteAsync(runDir).ConfigureAwait(false);
        }
    }

    private bool TryFindToolFile(string? toolName, out string path, out string error)
    {
        path = string.Empty;
        if (_toolsDir is null || !Directory.Exists(_toolsDir))
        {
            error = "Error: tool directory was not found.";
            return false;
        }

        var stem = NormalizeToolName(toolName);
        if (stem is null)
        {
            error = "tool_name must be a file stem such as ReadFileTool (letters, digits, underscore; no path).";
            return false;
        }

        var want = stem + ".cs";
        var matches = Directory.EnumerateFiles(_toolsDir, "*.cs", SearchOption.AllDirectories)
            .Where(p => string.Equals(Path.GetFileName(p), want, PathComparison))
            .Where(p => !IsBuildOutput(p) && IsUnderTools(p))
            .ToList();

        if (matches.Count == 0)
        {
            error = $"Error: Tool {stem} not found in {_toolsDir}. New files are not created; only an existing tool source can be replaced.";
            return false;
        }

        if (matches.Count > 1)
        {
            var listed = string.Join(", ", matches.Select(p => Path.GetRelativePath(_toolsDir, p)));
            error = $"Error: Tool {stem} matches more than one file: {listed}";
            return false;
        }

        path = matches[0];
        error = string.Empty;
        return true;
    }

    private bool IsUnderTools(string path)
    {
        if (_toolsDir is null)
            return false;
        var root = _toolsDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var final = FinalPath(path);
        return final.StartsWith(root, PathComparison);
    }

    private static bool IsBuildOutput(string path)
    {
        return path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", PathComparison)
            || path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", PathComparison);
    }

    private static string? FindPackageFromCaller(string callerFile)
    {
        if (string.IsNullOrWhiteSpace(callerFile))
            return null;

        DirectoryInfo? dir;
        try
        {
            var start = Path.GetDirectoryName(callerFile);
            dir = string.IsNullOrWhiteSpace(start) ? null : new DirectoryInfo(start);
        }
        catch (Exception)
        {
            return null;
        }

        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "SoulCore.sln"))
                && Directory.Exists(Path.Combine(dir.FullName, "scripts")))
                return dir.FullName;

            var nested = Path.Combine(dir.FullName, "SoulCore");
            if (File.Exists(Path.Combine(nested, "SoulCore.sln"))
                && Directory.Exists(Path.Combine(nested, "scripts")))
                return nested;

            dir = dir.Parent;
        }

        return null;
    }

    private static string? NormalizeToolName(string? toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            return null;
        var stem = toolName.Trim();
        if (stem.EndsWith(".cs", PathComparison))
            stem = stem[..^3];
        if (stem.Length is 0 or > 80)
            return null;
        if (!ToolNamePattern().IsMatch(stem))
            return null;
        return stem;
    }

    private static string FinalDirectory(string path)
    {
        var full = Path.GetFullPath(path);
        var target = Directory.ResolveLinkTarget(full, returnFinalTarget: true);
        return target?.FullName ?? full;
    }

    private static string FinalPath(string path)
    {
        var full = Path.GetFullPath(path);
        var target = File.Exists(full) ? File.ResolveLinkTarget(full, returnFinalTarget: true) : null;
        return target?.FullName ?? full;
    }

    private static string StripFences(string code)
    {
        var trimmed = code.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
            return code;
        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline < 0)
            return code;
        trimmed = trimmed[(firstNewline + 1)..];
        if (trimmed.EndsWith("```", StringComparison.Ordinal))
            trimmed = trimmed[..^3];
        return trimmed.Trim() + Environment.NewLine;
    }

    private static bool CopyBuildOutputs(string sandboxRoot, string libDir)
    {
        Directory.CreateDirectory(libDir);
        var copied = 0;
        foreach (var dll in Directory.EnumerateFiles(sandboxRoot, "*.dll", SearchOption.AllDirectories))
        {
            if (dll.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", PathComparison))
                continue;
            if (dll.Contains($"{Path.DirectorySeparatorChar}lib{Path.DirectorySeparatorChar}", PathComparison))
                continue;
            var dest = Path.Combine(libDir, Path.GetFileName(dll));
            if (File.Exists(dest))
                continue;
            File.Copy(dll, dest);
            copied++;
        }
        return copied > 0 && File.Exists(Path.Combine(libDir, "SoulCore.Inference.dll"));
    }

    private static string BuildSnippetProject(string libDir)
    {
        var refs = new StringBuilder();
        foreach (var dll in Directory.EnumerateFiles(libDir, "*.dll"))
        {
            var name = Path.GetFileNameWithoutExtension(dll);
            refs.Append("    <Reference Include=\"");
            refs.Append(XmlEscape(name));
            refs.Append("\"><HintPath>");
            refs.Append(XmlEscape(dll));
            refs.Append("</HintPath><Private>true</Private></Reference>");
            refs.AppendLine();
        }

        return $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net8.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
              </PropertyGroup>
              <ItemGroup>
                <Compile Include="Program.cs" />
            {refs}  </ItemGroup>
            </Project>
            """;
    }

    private static string FormatOutput(ToolProcessResult result)
    {
        var text = $"STDOUT:\n{result.Stdout}\nSTDERR:\n{result.Stderr}";
        if (text.Length > 24_000)
            text = text[..24_000] + "\n...[truncated]";
        return text;
    }

    private static string XmlEscape(string value)
        => value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);

    private static async Task TryDeleteAsync(string dir)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 2)
            {
                await Task.Delay(200).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException) when (attempt < 2)
            {
                await Task.Delay(200).ConfigureAwait(false);
            }
            catch (Exception)
            {
                return;
            }
        }
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex ToolNamePattern();
}
