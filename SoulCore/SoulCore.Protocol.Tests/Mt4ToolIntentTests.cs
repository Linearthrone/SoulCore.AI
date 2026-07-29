using SoulCore.Inference.Tools.Trading;
using SoulCore.Inference.Tools.Workflow;

namespace SoulCore.Protocol.Tests;

/// <summary>BED-167 / ISSUE-20260729-003 — NL MT4 status intent → ForceToolName=mt4_status.</summary>
public class Mt4ToolIntentTests
{
    [Theory]
    [InlineData("what's my MT4 status?", "mt4_status")]
    [InlineData("What's my MT4 status?", "mt4_status")]
    [InlineData("what is my mt4 status", "mt4_status")]
    [InlineData("MT4 status", "mt4_status")]
    [InlineData("show MetaTrader status please", "mt4_status")]
    [InlineData("is my MT4 connected?", "mt4_status")]
    [InlineData("check the MT4 bridge", "mt4_status")]
    [InlineData("You must call the mt4_status tool now.", "mt4_status")]
    [InlineData("call mt4_status", "mt4_status")]
    public void TryMatch_Ac4Prompts_ReturnsMt4Status(string text, string expectedTool)
    {
        Assert.True(Mt4ToolIntent.TryMatch(text, out var match));
        Assert.Equal(expectedTool, match.ToolName);
        Assert.Equal(Mt4ToolIntent.Kind.Status, match.Intent);
    }

    [Theory]
    [InlineData("what's the status of that task?")]
    [InlineData("create a task to remember the charter")]
    [InlineData("run that workflow")]
    [InlineData("hello victoria")]
    [InlineData("list all my tasks")]
    [InlineData("")]
    [InlineData(null)]
    public void TryMatch_NonMt4_ReturnsFalse(string? text)
    {
        Assert.False(Mt4ToolIntent.TryMatch(text, out _));
    }

    [Fact]
    public void AgencyGuidance_MentionsMt4Status_NotTaskTools()
    {
        var once = ToolAgencyGuidance.AppendToPreamble("hello");
        Assert.Contains("mt4_status", once, StringComparison.Ordinal);
        Assert.Contains("task_create", once, StringComparison.Ordinal);
        Assert.Contains("task_get", once, StringComparison.Ordinal);
        Assert.Contains("MT4", once, StringComparison.Ordinal);
    }

    [Fact]
    public void Mt4StatusTool_Description_DisambiguatesFromTaskTools()
    {
        var def = new Mt4StatusTool(
            new NullMt4Bridge(),
            Microsoft.Extensions.Options.Options.Create(new SoulCore.Config.ToolsOptions())).Definition;

        Assert.Equal("mt4_status", def.Name);
        Assert.Contains("what's my MT4 status?", def.Description, StringComparison.Ordinal);
        Assert.Contains("task_create", def.Description, StringComparison.Ordinal);
        Assert.Contains("task_get", def.Description, StringComparison.Ordinal);
    }

    private sealed class NullMt4Bridge : SoulCore.Inference.Tools.Trading.IMt4Bridge
    {
        public Task<SoulCore.Inference.ToolResult> InvokeAsync(
            string mcpToolName,
            System.Text.Json.JsonElement args,
            CancellationToken ct = default) =>
            Task.FromResult(new SoulCore.Inference.ToolResult(true, "{}", null));
    }
}
