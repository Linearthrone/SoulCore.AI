using System.Text.Json;
using SoulCore.Inference.Tools.ChiefArchitect;

namespace SoulCore.Protocol.Tests;

public class ChiefArchitectToolsTests
{
    private static string PlaybookRoot()
    {
        var repo = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..",
            "SoulCore.Inference", "Tools", "ChiefArchitect", "playbooks"));
        if (Directory.Exists(repo)) return repo;

        var outCopy = Path.Combine(AppContext.BaseDirectory, "Tools", "ChiefArchitect", "playbooks");
        if (Directory.Exists(outCopy)) return outCopy;

        return CaPlaybookLibrary.DefaultPlaybookRoot();
    }

    [Fact]
    public void BriefCompiler_3BrSlab_ParsesFootprint()
    {
        var p = CaBriefCompiler.Compile("3 bedroom single story on a slab, 40x30 ranch");
        Assert.Equal(3, p.Bedrooms);
        Assert.Equal(1, p.Stories);
        Assert.Equal("slab", p.Foundation);
        Assert.Equal(40, p.WidthFt);
        Assert.Equal(30, p.DepthFt);
        Assert.Equal("ranch", p.Style);
        Assert.Contains(p.Rooms, r => r.Contains("Bedroom", StringComparison.OrdinalIgnoreCase)
            || r.Contains("Master", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PlaybookLibrary_LoadsRecipes_AndPlans()
    {
        var lib = new CaPlaybookLibrary(PlaybookRoot());
        var playbook = lib.GetResidentialSlabPlaybook();
        Assert.Equal("residential.single_story.slab", playbook.Id);
        Assert.Contains(playbook.Stages, s => s.Id == "foundation");

        var wall = lib.GetRecipe("wall.straight_exterior.activate");
        Assert.Contains("Straight Exterior Wall", wall.Menu!);
        Assert.Contains("desktop_click", wall.DesktopTools);

        var drag = lib.GetRecipe("wall.draw_perimeter_rect");
        Assert.Contains("desktop_drag", drag.DesktopTools);

        var foundation = lib.GetRecipe("foundation.build_dialog");
        Assert.Contains("Build Foundation", string.Join(" > ", foundation.Menu!));

        var program = CaBriefCompiler.Compile("3BR single story on a slab 40x30");
        var planJson = JsonSerializer.Serialize(lib.PlanProject(program));
        Assert.Contains("sketch_then_dimension", planJson);
        Assert.Contains("wall.straight_exterior.activate", planJson);
        Assert.DoesNotContain("freehand slab from 0,0", planJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CaPlanAndNextStep_AdvancesThroughRecipes()
    {
        var lib = new CaPlaybookLibrary(PlaybookRoot());
        var session = new CaSessionState();
        var planTool = new CaPlanProjectTool(lib, session);
        var nextTool = new CaNextStepTool(lib, session);

        var planned = await planTool.ExecuteAsync(
            JsonDocument.Parse("""{"brief":"3 bedroom single story on a slab"}""").RootElement);
        Assert.True(planned.Success);
        Assert.Contains("sessionId", planned.Content);

        var step1 = await nextTool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        Assert.True(step1.Success);
        Assert.Contains("session.focus", step1.Content);

        var step2 = await nextTool.ExecuteAsync(
            JsonDocument.Parse("""{"mark_done":"session.focus"}""").RootElement);
        Assert.True(step2.Success);
        Assert.Contains("session.new_plan", step2.Content);
    }

    [Fact]
    public async Task CaGetRecipe_And_VerifyChecklist()
    {
        var lib = new CaPlaybookLibrary(PlaybookRoot());
        var get = new CaGetRecipeTool(lib);
        var verify = new CaVerifyChecklistTool(lib);

        var recipe = await get.ExecuteAsync(
            JsonDocument.Parse("""{"recipe_id":"foundation.choose_slab"}""").RootElement);
        Assert.True(recipe.Success);
        Assert.Contains("slab", recipe.Content, StringComparison.OrdinalIgnoreCase);

        var check = await verify.ExecuteAsync(
            JsonDocument.Parse("""{"stage_id":"perimeter"}""").RootElement);
        Assert.True(check.Success);
        Assert.Contains("checklist", check.Content);
    }

    [Fact]
    public void Guidance_And_Intent()
    {
        var once = ChiefArchitectGuidance.AppendToPreamble("hi");
        Assert.Contains(ChiefArchitectGuidance.Marker, once);
        Assert.Equal(once, ChiefArchitectGuidance.AppendToPreamble(once));

        Assert.True(ChiefArchitectToolIntent.TryMatch(
            "draw a 3 bedroom single story on a slab", out var m));
        Assert.Equal("ca_plan_project", m.ToolName);

        Assert.False(ChiefArchitectToolIntent.TryMatch("what's the weather?", out _));
    }

    [Fact]
    public async Task CaWorldHint_DefaultSketchStrategy()
    {
        var tool = new CaWorldHintTool();
        var result = await tool.ExecuteAsync(
            JsonDocument.Parse("""{"width_ft":40,"depth_ft":30}""").RootElement);
        Assert.True(result.Success);
        Assert.Contains("sketch_then_dimension", result.Content);
    }
}
