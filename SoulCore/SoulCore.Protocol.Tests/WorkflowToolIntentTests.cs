using System.Text.Json;
using SoulCore.Inference.Tools.Workflow;

namespace SoulCore.Protocol.Tests;

/// <summary>BED-162 / ISSUE-20260729-001 — NL workflow intent + agency guidance.</summary>
public class WorkflowToolIntentTests
{
    [Theory]
    [InlineData("create a workflow to: 1) recall a memory, 2) speak the memory", "workflow_create")]
    [InlineData("Create a Workflow named morning", "workflow_create")]
    [InlineData("please create an workflow for chores", "workflow_create")]
    [InlineData("run that workflow", "workflow_execute")]
    [InlineData("run that workflow again", "workflow_execute")]
    [InlineData("Execute the workflow", "workflow_execute")]
    [InlineData("start this workflow now", "workflow_execute")]
    public void TryMatch_AcPrompts_ReturnsExpectedTool(string text, string expectedTool)
    {
        Assert.True(WorkflowToolIntent.TryMatch(text, out var match));
        Assert.Equal(expectedTool, match.ToolName);
    }

    [Theory]
    [InlineData("what's the weather?")]
    [InlineData("list all my tasks")]
    [InlineData("create a task to remember the charter")]
    [InlineData("")]
    [InlineData(null)]
    public void TryMatch_NonWorkflow_ReturnsFalse(string? text)
    {
        Assert.False(WorkflowToolIntent.TryMatch(text, out _));
    }

    [Theory]
    [InlineData("Recall a specific memory.", "recall_memory")]
    [InlineData("speak the recalled memory", "speak")]
    [InlineData("Say aloud the memory", "speak")]
    [InlineData("store a memory about charter", "store_memory")]
    [InlineData("walk to the window", null)]
    public void InferToolFromDescription_KnownPhrases(string description, string? expected)
    {
        Assert.Equal(expected, WorkflowToolIntent.InferToolFromDescription(description));
    }

    [Fact]
    public void AgencyGuidance_AppendsOnce_Idempotent()
    {
        var once = ToolAgencyGuidance.AppendToPreamble("hello");
        Assert.Contains("[Tools]", once, StringComparison.Ordinal);
        Assert.Contains("workflow_create", once, StringComparison.Ordinal);
        Assert.Contains("workflow_execute", once, StringComparison.Ordinal);
        Assert.Contains("mt4_status", once, StringComparison.Ordinal);

        var twice = ToolAgencyGuidance.AppendToPreamble(once);
        Assert.Equal(once, twice);
    }

    [Fact]
    public void TryFindLatestWorkflowId_FromCreatedToolResult()
    {
        Assert.True(WorkflowToolIntent.TryFindLatestWorkflowId(
            new[] { "created: id=7 name=ac5 steps=2", "run that workflow" },
            null,
            out var id));
        Assert.Equal(7, id);
    }

    [Fact]
    public void TryFindLatestWorkflowId_PrefersNewest_AndArgsObject()
    {
        var older = JsonDocument.Parse("""{"id":3,"all":true}""").RootElement.Clone();
        var newer = JsonDocument.Parse("""{"id":11}""").RootElement.Clone();
        Assert.True(WorkflowToolIntent.TryFindLatestWorkflowId(
            new[] { "workflow id=3 name=x", "created: id=11 name=y steps=1" },
            new JsonElement?[] { older, newer },
            out var id));
        Assert.Equal(11, id);
    }

    [Fact]
    public void TryFindLatestWorkflowId_Missing_ReturnsFalse()
    {
        Assert.False(WorkflowToolIntent.TryFindLatestWorkflowId(
            new[] { "hello", "no ids here" },
            null,
            out _));
    }
}
