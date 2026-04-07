using System.Text.Json;
using System.Text.Json.Nodes;
using McpToolkit.Core;
using McpToolkit.Core.Abstractions;
using McpToolkit.Core.Models;
using Microsoft.Extensions.Logging;

namespace McpToolkit.Server;

/// <summary>
/// Hosts MCP tools over stdin/stdout using JSON-RPC 2.0.
/// Compatible with Claude Desktop, Claude Code, and any MCP client.
/// Add to your console app and call RunAsync() — that's it.
/// </summary>
public sealed class McpServer : IMcpServer
{
    private readonly List<BuiltMcpTool> _tools = [];
    private readonly ILogger<McpServer> _logger;

    public McpServerInfo ServerInfo { get; }

    public McpServer(McpServerInfo serverInfo, ILogger<McpServer> logger)
    {
        ServerInfo = serverInfo;
        _logger = logger;
    }

    /// <summary>Register a tool built with McpToolBuilder.</summary>
    public McpServer AddTool(BuiltMcpTool tool)
    {
        _tools.Add(tool);
        _logger.LogDebug("Registered MCP tool: {Name}", tool.Definition.Name);
        return this;
    }

    /// <summary>Register a tool using a factory function — call McpToolBuilder.Create() inside.</summary>
    public McpServer AddTool(Func<BuiltMcpTool> build) => AddTool(build());

    public IReadOnlyList<McpTool> GetTools() =>
        _tools.Select(t => t.Definition).ToList().AsReadOnly();

    public async Task<McpToolResult> CallToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken ct = default)
    {
        var tool = _tools.FirstOrDefault(t => t.Definition.Name == toolName);
        if (tool is null)
            return new McpToolResult([McpContent.FromError($"Unknown tool: {toolName}")], IsError: true);

        try
        {
            return await tool.ExecuteAsync(arguments, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool {Name} threw an unhandled exception", toolName);
            return new McpToolResult([McpContent.FromError(ex.Message)], IsError: true);
        }
    }

    /// <summary>
    /// Start the MCP server. Reads JSON-RPC from stdin, writes to stdout.
    /// Blocks until stdin closes or ct is cancelled.
    /// </summary>
    public async Task RunAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("McpToolkit.Server starting — {Name} v{Version}",
            ServerInfo.Name, ServerInfo.Version);

        using var stdin  = new StreamReader(Console.OpenStandardInput());
        using var stdout = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };

        while (!ct.IsCancellationRequested)
        {
            var line = await stdin.ReadLineAsync(ct);
            if (line is null) break;
            if (string.IsNullOrWhiteSpace(line)) continue;

            string? response;
            try
            {
                response = await HandleMessageAsync(line, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message");
                response = JsonRpc.Error(null, -32603, "Internal error: " + ex.Message);
            }

            if (response is not null)
                await stdout.WriteLineAsync(response);
        }
    }

    private async Task<string?> HandleMessageAsync(string json, CancellationToken ct)
    {
        var node    = JsonNode.Parse(json);
        if (node is null) return JsonRpc.Error(null, -32700, "Parse error");

        var id      = node["id"];
        var method  = node["method"]?.GetValue<string>();
        var @params = node["params"];

        return method switch
        {
            "initialize"  => HandleInitialize(id),
            "initialized" => null,
            "tools/list"  => HandleToolsList(id),
            "tools/call"  => await HandleToolsCallAsync(id, @params, ct),
            "ping"        => JsonRpc.Result(id, new { }),
            _             => JsonRpc.Error(id, -32601, $"Method not found: {method}")
        };
    }

    private string HandleInitialize(JsonNode? id) =>
        JsonRpc.Result(id, new
        {
            protocolVersion = "2024-11-05",
            serverInfo      = new { name = ServerInfo.Name, version = ServerInfo.Version },
            capabilities    = new
            {
                tools     = ServerInfo.Capabilities.Tools     ? new { } : (object?)null,
                resources = ServerInfo.Capabilities.Resources ? new { } : null,
                prompts   = ServerInfo.Capabilities.Prompts   ? new { } : null,
            }
        });

    private string HandleToolsList(JsonNode? id) =>
        JsonRpc.Result(id, new { tools = GetTools() });

    private async Task<string> HandleToolsCallAsync(
        JsonNode? id, JsonNode? @params, CancellationToken ct)
    {
        var toolName  = @params?["name"]?.GetValue<string>()
            ?? throw new ArgumentException("tools/call missing 'name'");
        var argsNode  = @params?["arguments"];
        var arguments = ParseArguments(argsNode);
        var result    = await CallToolAsync(toolName, arguments, ct);
        return JsonRpc.Result(id, result);
    }

    private static Dictionary<string, object?> ParseArguments(JsonNode? node)
    {
        if (node is not JsonObject obj) return [];
        return obj.ToDictionary(kv => kv.Key, kv => (object?)(kv.Value?.ToString()));
    }
}

internal static class JsonRpc
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition      = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static string Result(JsonNode? id, object result) =>
        JsonSerializer.Serialize(new { jsonrpc = "2.0", id, result }, _opts);

    public static string Error(JsonNode? id, int code, string message) =>
        JsonSerializer.Serialize(new { jsonrpc = "2.0", id, error = new { code, message } }, _opts);
}
