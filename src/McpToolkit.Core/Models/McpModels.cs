using System.Text.Json.Serialization;

namespace McpToolkit.Core.Models;

/// <summary>Represents an MCP Tool that an LLM can invoke.</summary>
public sealed record McpTool(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("inputSchema")] JsonSchema InputSchema
);

/// <summary>JSON Schema definition for a tool's input parameters.</summary>
public sealed record JsonSchema(
    [property: JsonPropertyName("type")]       string Type,
    [property: JsonPropertyName("properties")] Dictionary<string, JsonSchemaProperty> Properties,
    [property: JsonPropertyName("required")]   string[]? Required = null
);

/// <summary>A single JSON Schema property descriptor.</summary>
public sealed record JsonSchemaProperty(
    [property: JsonPropertyName("type")]        string Type,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("enum")]        string[]? Enum = null
);

/// <summary>Result returned from a tool invocation.</summary>
public sealed record McpToolResult(
    [property: JsonPropertyName("content")] IReadOnlyList<McpContent> Content,
    [property: JsonPropertyName("isError")] bool IsError = false
);

/// <summary>Content block in an MCP message — text, image, or resource.</summary>
public sealed record McpContent(
    [property: JsonPropertyName("type")]     string Type,
    [property: JsonPropertyName("text")]     string? Text = null,
    [property: JsonPropertyName("mimeType")] string? MimeType = null,
    [property: JsonPropertyName("data")]     string? Data = null
)
{
    public static McpContent FromText(string text)   => new("text", Text: text);
    public static McpContent FromError(string error) => new("text", Text: $"Error: {error}");
}

/// <summary>MCP Resource that LLMs can read.</summary>
public sealed record McpResource(
    [property: JsonPropertyName("uri")]         string Uri,
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("mimeType")]    string MimeType = "text/plain"
);

/// <summary>MCP Prompt template for reusable prompt patterns.</summary>
public sealed record McpPrompt(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("arguments")]   IReadOnlyList<McpPromptArgument>? Arguments = null
);

/// <summary>An argument in an MCP Prompt template.</summary>
public sealed record McpPromptArgument(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("required")]    bool Required = false
);

/// <summary>Server capabilities advertised to connected clients.</summary>
public sealed record McpServerInfo(
    [property: JsonPropertyName("name")]         string Name,
    [property: JsonPropertyName("version")]      string Version,
    [property: JsonPropertyName("capabilities")] McpCapabilities Capabilities
);

/// <summary>Feature flags for what this server supports.</summary>
public sealed record McpCapabilities(
    [property: JsonPropertyName("tools")]     bool Tools = true,
    [property: JsonPropertyName("resources")] bool Resources = false,
    [property: JsonPropertyName("prompts")]   bool Prompts = false
);
