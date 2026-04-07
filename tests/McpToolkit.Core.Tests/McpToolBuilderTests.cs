using FluentAssertions;
using McpToolkit.Core;
using McpToolkit.Core.Models;
using Xunit;

namespace McpToolkit.Core.Tests;

public class McpToolBuilderTests
{
    [Fact]
    public void Build_WithAllRequired_ProducesCorrectDefinition()
    {
        var tool = McpToolBuilder
            .Create("test_tool")
            .WithDescription("A test tool")
            .AddParameter("input", "The input string")
            .ExecutesWith(async (args, ct) =>
                new McpToolResult([McpContent.FromText("ok")]))
            .Build();

        tool.Definition.Name.Should().Be("test_tool");
        tool.Definition.Description.Should().Be("A test tool");
        tool.Definition.InputSchema.Properties.Should().ContainKey("input");
        tool.Definition.InputSchema.Required.Should().Contain("input");
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsExpectedResult()
    {
        var tool = McpToolBuilder
            .Create("echo")
            .WithDescription("Echoes input")
            .AddParameter("message", "Message to echo")
            .ExecutesWith(async (args, _) =>
            {
                var msg = args["message"]?.ToString() ?? "";
                return new McpToolResult([McpContent.FromText($"Echo: {msg}")]);
            })
            .Build();

        var result = await tool.ExecuteAsync(
            new Dictionary<string, object?> { ["message"] = "hello" });

        result.IsError.Should().BeFalse();
        result.Content[0].Text.Should().Be("Echo: hello");
    }

    [Fact]
    public void Build_MissingName_Throws()
    {
        var act = () => McpToolBuilder
            .Create("")
            .WithDescription("desc")
            .ExecutesWith(async (_, _) => new McpToolResult([]))
            .Build();

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void McpContent_FromText_SetsCorrectType()
    {
        var content = McpContent.FromText("hello");
        content.Type.Should().Be("text");
        content.Text.Should().Be("hello");
    }

    [Fact]
    public void McpContent_FromError_PrefixesMessage()
    {
        var content = McpContent.FromError("something broke");
        content.Text.Should().StartWith("Error:");
    }
}
