using System.Text.Json;
using System.Text.Json.Serialization;

namespace SoulCore.Inference.Tools.ChiefArchitect;

internal static class CaJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public static ToolResult Ok(object payload) =>
        new(Success: true, JsonSerializer.Serialize(payload, Options), payload);

    public static ToolResult Fail(string message) =>
        new(Success: false, message);
}

/// <summary><c>ca_compile_brief</c> - parse NL brief into a structured program.</summary>
public sealed class CaCompileBriefTool : ITool
{
    private static readonly JsonElement ParametersSchema = JsonDocument.Parse(
        """
        {
          "type": "object",
          "properties": {
            "brief": {
              "type": "string",
              "description": "Natural-language project brief, e.g. '3 bedroom single story on a slab, 40x30'."
            }
          },
          "required": ["brief"]
        }
        """).RootElement.Clone();

    public ToolDefinition Definition { get; } = new(
        Name: "ca_compile_brief",
        Description:
            "Compile a Chief Architect project brief into a structured program " +
            "(bedrooms, stories, foundation, footprint, rooms). Does not move the mouse.",
        Parameters: ParametersSchema);

    public Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        var brief = "";
        if (args.ValueKind == JsonValueKind.Object
            && args.TryGetProperty("brief", out var b)
            && b.ValueKind == JsonValueKind.String)
        {
            brief = b.GetString() ?? "";
        }

        if (string.IsNullOrWhiteSpace(brief))
            return Task.FromResult(CaJson.Fail("error: ca_compile_brief requires 'brief' (string)."));

        var program = CaBriefCompiler.Compile(brief);
        return Task.FromResult(CaJson.Ok(new { program }));
    }
}

/// <summary><c>ca_plan_project</c> - emit staged recipes and start a CA session.</summary>
public sealed class CaPlanProjectTool : ITool
{
    private static readonly JsonElement ParametersSchema = JsonDocument.Parse(
        """
        {
          "type": "object",
          "properties": {
            "brief": {
              "type": "string",
              "description": "Natural-language brief (compiled if program omitted)."
            }
          },
          "required": ["brief"]
        }
        """).RootElement.Clone();

    private readonly CaPlaybookLibrary _lib;
    private readonly CaSessionState _session;

    public ToolDefinition Definition { get; } = new(
        Name: "ca_plan_project",
        Description:
            "Plan a Chief Architect X17 residential build as ordered stages with recipe IDs " +
            "(walls first, then Build Foundation -> slab). Starts a CA session for ca_next_step. " +
            "Does not move the mouse - execute recipes with desktop_* tools.",
        Parameters: ParametersSchema);

    public CaPlanProjectTool(CaPlaybookLibrary lib, CaSessionState session)
    {
        _lib = lib ?? throw new ArgumentNullException(nameof(lib));
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        try
        {
            var brief = "";
            if (args.ValueKind == JsonValueKind.Object
                && args.TryGetProperty("brief", out var b)
                && b.ValueKind == JsonValueKind.String)
            {
                brief = b.GetString() ?? "";
            }

            if (string.IsNullOrWhiteSpace(brief))
                return Task.FromResult(CaJson.Fail("error: ca_plan_project requires 'brief' (string)."));

            var program = CaBriefCompiler.Compile(brief);
            var playbook = _lib.GetResidentialSlabPlaybook();
            var sessionId = _session.Start(program, playbook);
            var plan = _lib.PlanProject(program);
            return Task.FromResult(CaJson.Ok(new { sessionId, plan }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(CaJson.Fail("error: ca_plan_project failed: " + ex.Message));
        }
    }
}

/// <summary><c>ca_get_recipe</c> - return one action recipe by id.</summary>
public sealed class CaGetRecipeTool : ITool
{
    private static readonly JsonElement ParametersSchema = JsonDocument.Parse(
        """
        {
          "type": "object",
          "properties": {
            "recipe_id": {
              "type": "string",
              "description": "Recipe id, e.g. wall.straight_exterior.activate"
            }
          },
          "required": ["recipe_id"]
        }
        """).RootElement.Clone();

    private readonly CaPlaybookLibrary _lib;

    public ToolDefinition Definition { get; } = new(
        Name: "ca_get_recipe",
        Description:
            "Return one Chief Architect action recipe (menu path, gesture, desktop_tools, verify). " +
            "Does not move the mouse.",
        Parameters: ParametersSchema);

    public CaGetRecipeTool(CaPlaybookLibrary lib)
    {
        _lib = lib ?? throw new ArgumentNullException(nameof(lib));
    }

    public Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty("recipe_id", out var idEl)
            || idEl.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(idEl.GetString()))
        {
            return Task.FromResult(CaJson.Fail("error: ca_get_recipe requires 'recipe_id' (string)."));
        }

        var recipeId = idEl.GetString()!.Trim();
        if (!_lib.TryGetRecipe(recipeId, out var recipe) || recipe == null)
            return Task.FromResult(CaJson.Fail("error: unknown recipe_id '" + recipeId + "'"));

        return Task.FromResult(CaJson.Ok(new { recipe }));
    }
}

/// <summary><c>ca_next_step</c> - advance the active CA session.</summary>
public sealed class CaNextStepTool : ITool
{
    private static readonly JsonElement ParametersSchema = JsonDocument.Parse(
        """
        {
          "type": "object",
          "properties": {
            "mark_done": {
              "type": "string",
              "description": "Optional recipe id to mark complete before returning the next step."
            },
            "blocker": {
              "type": "string",
              "description": "Optional blocker note if the UI did not match."
            }
          }
        }
        """).RootElement.Clone();

    private readonly CaPlaybookLibrary _lib;
    private readonly CaSessionState _session;

    public ToolDefinition Definition { get; } = new(
        Name: "ca_next_step",
        Description:
            "Return the next Chief Architect recipe for the active session from ca_plan_project. " +
            "Optionally mark a recipe done. Does not move the mouse.",
        Parameters: ParametersSchema);

    public CaNextStepTool(CaPlaybookLibrary lib, CaSessionState session)
    {
        _lib = lib ?? throw new ArgumentNullException(nameof(lib));
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (args.ValueKind == JsonValueKind.Object)
        {
            if (args.TryGetProperty("mark_done", out var done)
                && done.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(done.GetString()))
            {
                _session.MarkRecipeDone(done.GetString()!.Trim());
            }

            if (args.TryGetProperty("blocker", out var blocker)
                && blocker.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(blocker.GetString()))
            {
                _session.AddBlocker(blocker.GetString()!);
            }
        }

        return Task.FromResult(CaJson.Ok(_session.NextStep(_lib)));
    }
}

/// <summary><c>ca_world_hint</c> - feet-to-pixel / sketch-then-dimension advice.</summary>
public sealed class CaWorldHintTool : ITool
{
    private static readonly JsonElement ParametersSchema = JsonDocument.Parse(
        """
        {
          "type": "object",
          "properties": {
            "width_ft": { "type": "number" },
            "depth_ft": { "type": "number" },
            "feet_per_pixel": {
              "type": "number",
              "description": "Optional calibration; omit to use sketch-then-dimension strategy."
            }
          }
        }
        """).RootElement.Clone();

    public ToolDefinition Definition { get; } = new(
        Name: "ca_world_hint",
        Description:
            "Advise how to map architectural feet to screen drags for Chief Architect. " +
            "Default: sketch topology with desktop_drag, then set exact lengths in CA.",
        Parameters: ParametersSchema);

    public Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        double? widthFt = null;
        double? depthFt = null;
        double? feetPerPixel = null;

        if (args.ValueKind == JsonValueKind.Object)
        {
            if (args.TryGetProperty("width_ft", out var w) && w.TryGetDouble(out var wv))
                widthFt = wv;
            if (args.TryGetProperty("depth_ft", out var d) && d.TryGetDouble(out var dv))
                depthFt = dv;
            if (args.TryGetProperty("feet_per_pixel", out var f) && f.TryGetDouble(out var fv))
                feetPerPixel = fv;
        }

        if (feetPerPixel is > 0.0)
        {
            int? widthPx = widthFt.HasValue
                ? (int)Math.Round(widthFt.Value / feetPerPixel.Value)
                : null;
            int? depthPx = depthFt.HasValue
                ? (int)Math.Round(depthFt.Value / feetPerPixel.Value)
                : null;

            return Task.FromResult(CaJson.Ok(new
            {
                strategy = "calibrated_drag",
                feetPerPixel,
                suggestedDragPixels = new { width = widthPx, depth = depthPx },
                note = "Still verify with CA dimensions after dragging."
            }));
        }

        return Task.FromResult(CaJson.Ok(new
        {
            strategy = "sketch_then_dimension",
            widthFt,
            depthFt,
            note =
                "Drag approximate sides with desktop_drag to close the perimeter. " +
                "Then select walls and type exact lengths. Do not invent a slab rectangle from 0,0."
        }));
    }
}

/// <summary><c>ca_verify_checklist</c> - post-stage screenshot checklist items.</summary>
public sealed class CaVerifyChecklistTool : ITool
{
    private static readonly JsonElement ParametersSchema = JsonDocument.Parse(
        """
        {
          "type": "object",
          "properties": {
            "stage_id": {
              "type": "string",
              "description": "Stage id: session|perimeter|dimensions|rooms|openings|foundation|verify"
            }
          },
          "required": ["stage_id"]
        }
        """).RootElement.Clone();

    private readonly CaPlaybookLibrary _lib;

    public ToolDefinition Definition { get; } = new(
        Name: "ca_verify_checklist",
        Description:
            "Return human/agent checklist items to confirm from desktop_screenshot after a CA stage. " +
            "Does not move the mouse.",
        Parameters: ParametersSchema);

    public CaVerifyChecklistTool(CaPlaybookLibrary lib)
    {
        _lib = lib ?? throw new ArgumentNullException(nameof(lib));
    }

    public Task<ToolResult> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty("stage_id", out var stageEl)
            || stageEl.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(stageEl.GetString()))
        {
            return Task.FromResult(CaJson.Fail("error: ca_verify_checklist requires 'stage_id'."));
        }

        var stageId = stageEl.GetString()!.Trim();
        var playbook = _lib.GetResidentialSlabPlaybook();
        var stage = playbook.Stages.FirstOrDefault(
            x => string.Equals(x.Id, stageId, StringComparison.OrdinalIgnoreCase));
        if (stage == null)
            return Task.FromResult(CaJson.Fail("error: unknown stage_id '" + stageId + "'"));

        var checklist = new List<string>();
        foreach (var recipeId in stage.RecipeIds)
        {
            if (_lib.TryGetRecipe(recipeId, out var recipe) && recipe != null)
                checklist.AddRange(recipe.Verify);
        }

        checklist = checklist.Distinct(StringComparer.Ordinal).ToList();
        return Task.FromResult(CaJson.Ok(new
        {
            stageId = stage.Id,
            stageTitle = stage.Title,
            checklist,
            instruction =
                "Call desktop_screenshot, visually confirm each checklist item, " +
                "then ca_next_step with mark_done for completed recipes. Stop if items fail."
        }));
    }
}
