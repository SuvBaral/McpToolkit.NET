using McpToolkit.Core.Models;

namespace McpToolkit.Core;

/// <summary>
/// Mark a method as an MCP tool for automatic registration.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class McpToolAttribute : Attribute
{
    public string? Name { get; init; }
    public required string Description { get; init; }
}

/// <summary>Mark a parameter to add a description to its JSON Schema entry.</summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class McpParamAttribute : Attribute
{
    public required string Description { get; init; }
    public bool Required { get; init; } = true;
    public string[]? AllowedValues { get; init; }
}

/// <summary>
/// Fluently define an MCP tool without writing a full class.
/// </summary>
public sealed class McpToolBuilder
{
    private string _name;
    private string _description = "";
    private readonly List<(string Name, JsonSchemaProperty Schema, bool Required)> _params = [];
    private Func<IReadOnlyDictionary<string, object?>, CancellationToken, Task<McpToolResult>>? _handler;

    private McpToolBuilder(string name) => _name = name;

    public static McpToolBuilder Create(string name) => new(name);

    public McpToolBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    public McpToolBuilder AddParameter(
        string name,
        string description,
        string type = "string",
        bool required = true,
        string[]? allowedValues = null)
    {
        _params.Add((name, new JsonSchemaProperty(type, description, allowedValues), required));
        return this;
    }

    public McpToolBuilder ExecutesWith(
        Func<IReadOnlyDictionary<string, object?>, CancellationToken, Task<McpToolResult>> handler)
    {
        _handler = handler;
        return this;
    }

    public McpToolBuilder ExecutesWith(
        Func<IReadOnlyDictionary<string, object?>, Task<McpToolResult>> handler)
    {
        _handler = (args, _) => handler(args);
        return this;
    }

    public BuiltMcpTool Build()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(_name);
        ArgumentException.ThrowIfNullOrWhiteSpace(_description);
        ArgumentNullException.ThrowIfNull(_handler);

        var properties = _params.ToDictionary(p => p.Name, p => p.Schema);
        var required   = _params.Where(p => p.Required).Select(p => p.Name).ToArray();
        var schema     = new JsonSchema("object", properties, required.Length > 0 ? required : null);
        var tool       = new McpTool(_name, _description, schema);

        return new BuiltMcpTool(tool, _handler);
    }
}

/// <summary>A compiled, ready-to-register MCP tool from the fluent builder.</summary>
public sealed class BuiltMcpTool(
    McpTool Definition,
    Func<IReadOnlyDictionary<string, object?>, CancellationToken, Task<McpToolResult>> Handler)
{
    public McpTool Definition { get; } = Definition;

    public Task<McpToolResult> ExecuteAsync(
        IReadOnlyDictionary<string, object?> args,
        CancellationToken ct = default) => Handler(args, ct);
}
