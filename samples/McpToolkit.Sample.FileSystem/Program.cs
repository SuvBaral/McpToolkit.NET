using McpToolkit.Core;
using McpToolkit.Core.Models;
using McpToolkit.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Sandboxed file system MCP server.
// Set ALLOWED_ROOTS env var to semicolon-separated list of allowed paths.

var allowedRoots = (Environment.GetEnvironmentVariable("ALLOWED_ROOTS")
    ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
    .Split(';', StringSplitOptions.RemoveEmptyEntries)
    .Select(Path.GetFullPath)
    .ToHashSet(StringComparer.OrdinalIgnoreCase);

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();

builder.Services.AddMcpServer("mcp-filesystem", "0.1.0", server =>
{
    server.AddTool(() => McpToolBuilder
        .Create("read_file")
        .WithDescription("Read the text contents of a file within allowed directories.")
        .AddParameter("path", "Absolute path to the file.")
        .ExecutesWith(async (args, ct) =>
        {
            var path = args["path"]?.ToString() ?? "";
            if (!IsAllowed(path)) return Err($"Access denied: {path}");
            try { return Ok($"```\n{await File.ReadAllTextAsync(path, ct)}\n```"); }
            catch (Exception ex) { return Err(ex.Message); }
        })
        .Build());

    server.AddTool(() => McpToolBuilder
        .Create("list_directory")
        .WithDescription("List files and subdirectories in a directory.")
        .AddParameter("path", "Absolute path to the directory.")
        .AddParameter("pattern", "Search pattern, e.g. '*.cs'. Default: '*'.", required: false)
        .ExecutesWith(async (args, _) =>
        {
            var path    = args["path"]?.ToString() ?? "";
            var pattern = args.GetValueOrDefault("pattern")?.ToString() ?? "*";
            if (!IsAllowed(path)) return Err("Access denied.");
            try
            {
                var dirs  = Directory.GetDirectories(path).Select(d => $"📁 {Path.GetFileName(d)}");
                var files = Directory.GetFiles(path, pattern).Select(f =>
                {
                    var fi = new FileInfo(f);
                    return $"📄 {fi.Name} ({FormatSize(fi.Length)})";
                });
                var all = string.Join("\n", dirs.Concat(files));
                return Ok(all.Length > 0 ? all : "_Empty directory._");
            }
            catch (Exception ex) { return Err(ex.Message); }
        })
        .Build());

    server.AddTool(() => McpToolBuilder
        .Create("write_file")
        .WithDescription("Write text to a file. Creates the file and parent directories if needed.")
        .AddParameter("path", "Absolute path to the file to write.")
        .AddParameter("content", "Text content to write.")
        .ExecutesWith(async (args, ct) =>
        {
            var path    = args["path"]?.ToString() ?? "";
            var content = args["content"]?.ToString() ?? "";
            if (!IsAllowed(path)) return Err("Access denied.");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(path, content, ct);
                return Ok($"Wrote {content.Length:N0} characters to {path}");
            }
            catch (Exception ex) { return Err(ex.Message); }
        })
        .Build());
});

await builder.Build().Services.GetRequiredService<McpServer>().RunAsync();

bool IsAllowed(string path)
{
    try
    {
        var full = Path.GetFullPath(path);
        return allowedRoots.Any(r => full.StartsWith(r, StringComparison.OrdinalIgnoreCase));
    }
    catch { return false; }
}

static string FormatSize(long b) => b switch
{
    < 1_024 => $"{b} B",
    < 1_048_576 => $"{b / 1_024} KB",
    _ => $"{b / 1_048_576} MB"
};

static McpToolResult Ok(string text)  => new([McpContent.FromText(text)]);
static McpToolResult Err(string msg)  => new([McpContent.FromError(msg)], IsError: true);
