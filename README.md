# McpToolkit.NET

> **The missing .NET SDK for the Model Context Protocol (MCP).**
> Build AI-callable tool servers in C# that work with Claude Desktop, Claude Code, and any MCP-compatible LLM client.

[![NuGet](https://img.shields.io/nuget/v/McpToolkit.Core.svg)](https://www.nuget.org/packages/McpToolkit.Core)
[![NuGet Downloads](https://img.shields.io/nuget/dt/McpToolkit.Core.svg)](https://www.nuget.org/packages/McpToolkit.Core)
[![CI](https://github.com/SuvBaral/McpToolkit.NET/actions/workflows/ci.yml/badge.svg)](https://github.com/SuvBaral/McpToolkit.NET/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

---

## What is MCP?

[Model Context Protocol](https://modelcontextprotocol.io) is an open standard by Anthropic that lets LLMs (Claude, GPT-4, etc.) call external tools, read resources, and use reusable prompts in a structured, safe way.

**MCP is to AI agents what REST is to web APIs.**

The official SDKs are Python and TypeScript only. The .NET ecosystem has nothing.
**McpToolkit.NET fixes that.**

---

## Quickstart
```bash
dotnet new console -n MyMcpServer
cd MyMcpServer
dotnet add package McpToolkit.Server
```
```csharp
// Program.cs — a complete working MCP server
using McpToolkit.Core.Models;
using McpToolkit.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders(); // MCP uses stdout — silence logs

builder.Services.AddMcpServer("my-server", "1.0.0", server =>
{
    server.AddTool(b => b
        .Create("get_time")
        .WithDescription("Returns the current UTC time.")
        .ExecutesWith(async (_, ct) =>
            new McpToolResult([McpContent.FromText(DateTime.UtcNow.ToString("R"))])));
});

await builder.Build().Services.GetRequiredService<McpServer>().RunAsync();
```

**Claude Desktop config** (`claude_desktop_config.json`):
```json
{
  "mcpServers": {
    "my-server": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/MyMcpServer"]
    }
  }
}
```

Restart Claude Desktop. Done. Claude can now call your tools.

---

## Real-World Example: SQL Server in 40 lines

Give Claude natural language access to your database:
```csharp
server.AddTool(b => b
    .Create("execute_query")
    .WithDescription("Run a read-only SQL SELECT query. Returns a markdown table.")
    .AddParameter("sql", "The SELECT statement to execute.")
    .ExecutesWith(async (args, ct) =>
    {
        var sql = args["sql"]?.ToString() ?? "";
        using var conn   = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        using var cmd    = new SqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        return new McpToolResult([McpContent.FromText(await FormatTable(reader, ct))]);
    }));
```

Ask Claude: *"Which customers spent the most last quarter? Group by region."*
Claude calls `list_tables` → `describe_table` → `execute_query`. Done.

---

## Packages

| Package | Description |
|---------|-------------|
| `McpToolkit.Core` | Protocol models, interfaces, fluent `McpToolBuilder` |
| `McpToolkit.Server` | stdio server — works with Claude Desktop + Claude Code |
| `McpToolkit.AspNetCore` | HTTP MCP server via ASP.NET Core *(coming soon)* |

---

## Samples

| Sample | What it does |
|--------|-------------|
| [`SqlServer`](samples/McpToolkit.Sample.SqlServer/) | Natural language → SQL queries |
| [`FileSystem`](samples/McpToolkit.Sample.FileSystem/) | Safe sandboxed file read/write/list |

---

## Roadmap

- [x] Core protocol models
- [x] stdio MCP server (Claude Desktop compatible)
- [x] Fluent `McpToolBuilder`
- [x] SQL Server sample
- [x] FileSystem sample
- [ ] `McpToolkit.AspNetCore` — HTTP transport
- [ ] `McpToolkit.EntityFramework` — natural language EF Core queries
- [ ] `[McpTool]` attribute + source generator
- [ ] `McpToolkit.Testing` — test helpers

---

## Contributing

PRs welcome. Open an issue first for large changes.

## License

MIT © [SuvBaral](https://github.com/SuvBaral)

---

*Built by a .NET developer, for .NET developers. Because Python shouldn't have all the fun.*
