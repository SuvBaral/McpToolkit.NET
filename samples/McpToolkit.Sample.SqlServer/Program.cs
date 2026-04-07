using McpToolkit.Core;
using McpToolkit.Core.Models;
using McpToolkit.Server;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// ─────────────────────────────────────────────────────────────────────
//  McpToolkit SQL Server Sample
//
//  Exposes 3 tools to Claude:
//    • list_tables   — show all tables with row counts
//    • describe_table — show columns for a table
//    • execute_query  — run a read-only SELECT and return markdown table
//
//  Claude Desktop config (~/.config/claude/claude_desktop_config.json):
//  {
//    "mcpServers": {
//      "sqlserver": {
//        "command": "dotnet",
//        "args": ["run", "--project", "/path/to/McpToolkit.Sample.SqlServer"],
//        "env": { "MSSQL_CONNECTION": "Server=.;Database=MyDb;Trusted_Connection=True;" }
//      }
//    }
//  }
// ─────────────────────────────────────────────────────────────────────

var connectionString =
    Environment.GetEnvironmentVariable("MSSQL_CONNECTION")
    ?? "Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True;";

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders(); // MCP uses stdout — no console logs allowed

builder.Services.AddMcpServer("mcp-sqlserver", "0.1.0", server =>
{
    // ── Tool 1: list_tables ────────────────────────────────────────
    server.AddTool(() => McpToolBuilder
        .Create("list_tables")
        .WithDescription(
            "List all user tables in the SQL Server database with their row counts. " +
            "Call this first to understand the database schema before writing queries.")
        .ExecutesWith(async (_, ct) =>
        {
            try
            {
                using var conn = new SqlConnection(connectionString);
                await conn.OpenAsync(ct);
                const string sql = """
                    SELECT
                        t.TABLE_SCHEMA + '.' + t.TABLE_NAME AS TableName,
                        p.rows AS RowCount,
                        (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS c
                         WHERE c.TABLE_NAME = t.TABLE_NAME
                           AND c.TABLE_SCHEMA = t.TABLE_SCHEMA) AS ColumnCount
                    FROM INFORMATION_SCHEMA.TABLES t
                    JOIN sys.partitions p
                        ON p.object_id = OBJECT_ID(t.TABLE_SCHEMA + '.' + t.TABLE_NAME)
                    WHERE t.TABLE_TYPE = 'BASE TABLE'
                      AND p.index_id IN (0,1)
                    ORDER BY p.rows DESC
                    """;
                using var cmd    = new SqlCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync(ct);
                return new McpToolResult([McpContent.FromText(await ToMarkdownAsync(reader, 500, ct))]);
            }
            catch (Exception ex)
            {
                return new McpToolResult([McpContent.FromError(ex.Message)], IsError: true);
            }
        })
        .Build());

    // ── Tool 2: describe_table ─────────────────────────────────────
    server.AddTool(() => McpToolBuilder
        .Create("describe_table")
        .WithDescription(
            "Get column definitions (name, type, nullable, default) for a specific table. " +
            "Use before writing queries to understand the schema.")
        .AddParameter("table_name", "Full table name including schema, e.g. 'dbo.Orders'")
        .ExecutesWith(async (args, ct) =>
        {
            var tableName = args["table_name"]?.ToString() ?? "";
            var parts  = tableName.Split('.', 2);
            var schema = parts.Length == 2 ? parts[0].Trim('[', ']') : "dbo";
            var table  = (parts.Length == 2 ? parts[1] : parts[0]).Trim('[', ']');

            try
            {
                using var conn = new SqlConnection(connectionString);
                await conn.OpenAsync(ct);
                const string sql = """
                    SELECT
                        COLUMN_NAME               AS [Column],
                        DATA_TYPE                 AS [Type],
                        CHARACTER_MAXIMUM_LENGTH  AS [MaxLen],
                        IS_NULLABLE               AS [Nullable],
                        COLUMN_DEFAULT            AS [Default]
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table
                    ORDER BY ORDINAL_POSITION
                    """;
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@schema", schema);
                cmd.Parameters.AddWithValue("@table", table);
                using var reader = await cmd.ExecuteReaderAsync(ct);
                return new McpToolResult([McpContent.FromText(await ToMarkdownAsync(reader, 500, ct))]);
            }
            catch (Exception ex)
            {
                return new McpToolResult([McpContent.FromError(ex.Message)], IsError: true);
            }
        })
        .Build());

    // ── Tool 3: execute_query ──────────────────────────────────────
    server.AddTool(() => McpToolBuilder
        .Create("execute_query")
        .WithDescription(
            "Execute a read-only SQL SELECT query against the database. " +
            "Returns results as a markdown table. Never use INSERT/UPDATE/DELETE.")
        .AddParameter("sql", "The SQL SELECT statement to execute.")
        .AddParameter("max_rows", "Maximum rows to return (1–1000, default 100).", required: false)
        .ExecutesWith(async (args, ct) =>
        {
            var sql     = args["sql"]?.ToString() ?? "";
            var maxRows = int.TryParse(args.GetValueOrDefault("max_rows")?.ToString(), out var n)
                ? Math.Clamp(n, 1, 1000) : 100;

            if (IsDangerous(sql))
                return new McpToolResult(
                    [McpContent.FromError("Only SELECT queries are permitted.")], IsError: true);

            try
            {
                using var conn = new SqlConnection(connectionString);
                await conn.OpenAsync(ct);
                using var cmd    = new SqlCommand(sql, conn) { CommandTimeout = 30 };
                using var reader = await cmd.ExecuteReaderAsync(ct);
                return new McpToolResult([McpContent.FromText(await ToMarkdownAsync(reader, maxRows, ct))]);
            }
            catch (Exception ex)
            {
                return new McpToolResult([McpContent.FromError($"SQL error: {ex.Message}")], IsError: true);
            }
        })
        .Build());
});

await builder.Build().Services.GetRequiredService<McpServer>().RunAsync();

// ── Helpers ───────────────────────────────────────────────────────

static bool IsDangerous(string sql)
{
    var up = sql.ToUpperInvariant();
    return new[] { "INSERT ", "UPDATE ", "DELETE ", "DROP ", "TRUNCATE ",
                   "ALTER ", "CREATE ", "EXEC ", "EXECUTE ", "MERGE " }
        .Any(k => up.Contains(k));
}

static async Task<string> ToMarkdownAsync(SqlDataReader r, int maxRows, CancellationToken ct)
{
    if (r.FieldCount == 0) return "_No columns returned._";
    var sb      = new System.Text.StringBuilder();
    var headers = Enumerable.Range(0, r.FieldCount).Select(r.GetName).ToArray();
    sb.AppendLine("| " + string.Join(" | ", headers) + " |");
    sb.AppendLine("| " + string.Join(" | ", headers.Select(_ => "---")) + " |");
    int count = 0;
    while (await r.ReadAsync(ct) && count < maxRows)
    {
        var cells = Enumerable.Range(0, r.FieldCount)
            .Select(i => r.IsDBNull(i) ? "_null_" : (r.GetValue(i).ToString() ?? "").Replace("|", "\\|"));
        sb.AppendLine("| " + string.Join(" | ", cells) + " |");
        count++;
    }
    if (count == maxRows) sb.AppendLine($"\n_Truncated at {maxRows} rows._");
    else if (count == 0)  sb.AppendLine("_No rows returned._");
    return sb.ToString();
}
