using McpToolkit.Core.Models;

namespace McpToolkit.Core.Abstractions;

/// <summary>
/// Implement this interface to expose a capability as an MCP Tool.
/// </summary>
public interface IMcpTool
{
    string Name { get; }
    string Description { get; }
    JsonSchema InputSchema { get; }
    Task<McpToolResult> ExecuteAsync(
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default);
}

/// <summary>Expose resources (files, DB records) that LLMs can read.</summary>
public interface IMcpResourceProvider
{
    Task<IReadOnlyList<McpResource>> ListResourcesAsync(CancellationToken ct = default);
    Task<McpContent> ReadResourceAsync(string uri, CancellationToken ct = default);
}

/// <summary>Expose reusable prompt templates.</summary>
public interface IMcpPromptProvider
{
    Task<IReadOnlyList<McpPrompt>> ListPromptsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<McpContent>> GetPromptAsync(
        string name,
        IReadOnlyDictionary<string, string> arguments,
        CancellationToken ct = default);
}

/// <summary>The top-level MCP server contract.</summary>
public interface IMcpServer
{
    McpServerInfo ServerInfo { get; }
    IReadOnlyList<McpTool> GetTools();
    Task<McpToolResult> CallToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken ct = default);
}
