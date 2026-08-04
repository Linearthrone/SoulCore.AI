using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SoulCore.Inference.Tools.ChiefArchitect;

/// <summary>Structured residential program compiled from a natural-language brief.</summary>
public sealed record CaProgram(
    int Bedrooms,
    int Stories,
    string Foundation,
    double? WidthFt,
    double? DepthFt,
    string? Style,
    IReadOnlyList<string> Rooms,
    string RawBrief);

/// <summary>Parses bedrooms, stories, foundation, footprint, style, and rooms from a brief.</summary>
public static class CaBriefCompiler
{
    private static readonly Regex Bedrooms = new(
        @"\b(\d+)\s*(?:br|bed(?:room)?s?)\b|\b(?:three|3)\s*bedroom",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex Stories = new(
        @"\b(?:single[\s-]?stor(?:y|ey)|1[\s-]?stor(?:y|ey)|one[\s-]?level)\b|\b(\d+)\s*stor(?:y|eys|ies)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex Slab = new(
        @"\b(?:on\s+a\s+)?slab\b|\bmonolithic\s+slab\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex Crawl = new(
        @"\bcrawl\s*space\b|\bcrawl\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex Basement = new(
        @"\bbasement\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex Footprint = new(
        @"\b(\d+(?:\.\d+)?)\s*(?:'|ft|feet)?\s*[xXby]\s*(\d+(?:\.\d+)?)\s*(?:'|ft|feet)?\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static CaProgram Compile(string? brief)
    {
        var text = (brief ?? "").Trim();
        if (text.Length == 0)
            text = "3 bedroom single story on a slab";

        var bedrooms = 3;
        var bedMatch = Bedrooms.Match(text);
        if (bedMatch.Success)
        {
            if (bedMatch.Groups[1].Success
                && int.TryParse(bedMatch.Groups[1].Value, out var n)
                && n > 0)
            {
                bedrooms = n;
            }
            else if (text.Contains("three", StringComparison.OrdinalIgnoreCase))
            {
                bedrooms = 3;
            }
        }

        var stories = 1;
        var storyMatch = Stories.Match(text);
        if (storyMatch.Success
            && storyMatch.Groups[1].Success
            && int.TryParse(storyMatch.Groups[1].Value, out var s)
            && s > 0)
        {
            stories = s;
        }
        else if (Regex.IsMatch(text, @"\bsingle[\s-]?stor", RegexOptions.IgnoreCase))
        {
            stories = 1;
        }

        var foundation = "slab";
        if (Basement.IsMatch(text) && !Slab.IsMatch(text))
            foundation = "basement";
        else if (Crawl.IsMatch(text) && !Slab.IsMatch(text))
            foundation = "crawl";
        else if (Slab.IsMatch(text))
            foundation = "slab";

        double? widthFt = null;
        double? depthFt = null;
        var foot = Footprint.Match(text);
        if (foot.Success
            && double.TryParse(foot.Groups[1].Value, out var w)
            && double.TryParse(foot.Groups[2].Value, out var d))
        {
            widthFt = w;
            depthFt = d;
        }
        else
        {
            widthFt = 40.0;
            depthFt = 30.0;
        }

        string? style = null;
        if (Regex.IsMatch(text, @"\branch\b", RegexOptions.IgnoreCase))
            style = "ranch";
        else if (Regex.IsMatch(text, @"\bcraftsman\b", RegexOptions.IgnoreCase))
            style = "craftsman";
        else if (Regex.IsMatch(text, @"\bmodern\b", RegexOptions.IgnoreCase))
            style = "modern";

        var rooms = new List<string> { "Living", "Kitchen", "Bath" };
        for (var i = 1; i <= bedrooms; i++)
            rooms.Add(i == 1 ? "Master Bedroom" : $"Bedroom {i}");
        if (bedrooms >= 2)
            rooms.Add("Bath 2");

        return new CaProgram(bedrooms, stories, foundation, widthFt, depthFt, style, rooms, text);
    }
}

public sealed class CaPlaybookStage
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("recipeIds")]
    public List<string> RecipeIds { get; set; } = new();
}

public sealed class CaPlaybookDoc
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("notes")]
    public List<string> Notes { get; set; } = new();

    [JsonPropertyName("stages")]
    public List<CaPlaybookStage> Stages { get; set; } = new();
}

public sealed class CaRecipe
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";

    [JsonPropertyName("menu")]
    public List<string>? Menu { get; set; }

    [JsonPropertyName("hotkey_hint")]
    public string? HotkeyHint { get; set; }

    [JsonPropertyName("ui")]
    public JsonElement? Ui { get; set; }

    [JsonPropertyName("gesture")]
    public JsonElement? Gesture { get; set; }

    [JsonPropertyName("desktop_tools")]
    public List<string> DesktopTools { get; set; } = new();

    [JsonPropertyName("params")]
    public List<string> Params { get; set; } = new();

    [JsonPropertyName("verify")]
    public List<string> Verify { get; set; } = new();
}

public sealed class CaRecipesDoc
{
    [JsonPropertyName("recipes")]
    public Dictionary<string, CaRecipe> Recipes { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>Loads residential_slab.json + recipes.json and plans staged CA builds.</summary>
public sealed class CaPlaybookLibrary
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly string _root;
    private CaPlaybookDoc? _slab;
    private CaRecipesDoc? _recipes;

    public CaPlaybookLibrary(string? rootDirectory = null)
    {
        _root = rootDirectory ?? DefaultPlaybookRoot();
    }

    public static string DefaultPlaybookRoot()
    {
        var asmDir = Path.GetDirectoryName(typeof(CaPlaybookLibrary).Assembly.Location) ?? ".";
        var besideAssembly = Path.Combine(asmDir, "Tools", "ChiefArchitect", "playbooks");
        if (Directory.Exists(besideAssembly))
            return besideAssembly;

        var candidates = new[]
        {
            Path.Combine(asmDir, "Tools", "ChiefArchitect", "playbooks"),
            Path.Combine(AppContext.BaseDirectory, "Tools", "ChiefArchitect", "playbooks"),
            Path.Combine(asmDir, "ChiefArchitect", "playbooks"),
            Path.Combine(AppContext.BaseDirectory, "ChiefArchitect", "playbooks"),
            Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..",
                "SoulCore.Inference", "Tools", "ChiefArchitect", "playbooks")),
            Path.GetFullPath(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".cursor", "plugins", "local", "chief-architect-x17", "playbooks"))
        };

        foreach (var c in candidates)
        {
            if (Directory.Exists(c))
                return c;
        }

        return besideAssembly;
    }

    public CaPlaybookDoc GetResidentialSlabPlaybook()
    {
        if (_slab != null)
            return _slab;

        var path = Path.Combine(_root, "residential_slab.json");
        if (!File.Exists(path))
            throw new FileNotFoundException("Chief Architect playbook missing: " + path);

        var json = File.ReadAllText(path);
        _slab = JsonSerializer.Deserialize<CaPlaybookDoc>(json, JsonOpts)
            ?? throw new InvalidOperationException("failed to parse residential_slab.json");
        return _slab;
    }

    public CaRecipe GetRecipe(string recipeId)
    {
        EnsureRecipes();
        if (!_recipes!.Recipes.TryGetValue(recipeId, out var value))
            throw new KeyNotFoundException("Unknown CA recipe '" + recipeId + "'");
        return value;
    }

    public bool TryGetRecipe(string recipeId, out CaRecipe? recipe)
    {
        EnsureRecipes();
        return _recipes!.Recipes.TryGetValue(recipeId, out recipe);
    }

    public IReadOnlyDictionary<string, CaRecipe> AllRecipes()
    {
        EnsureRecipes();
        return _recipes!.Recipes;
    }

    private void EnsureRecipes()
    {
        if (_recipes != null)
            return;

        var path = Path.Combine(_root, "recipes.json");
        if (!File.Exists(path))
            throw new FileNotFoundException("Chief Architect recipes missing: " + path);

        var json = File.ReadAllText(path);
        _recipes = JsonSerializer.Deserialize<CaRecipesDoc>(json, JsonOpts)
            ?? throw new InvalidOperationException("failed to parse recipes.json");
    }

    public object PlanProject(CaProgram program)
    {
        var playbook = GetResidentialSlabPlaybook();

        var stages = playbook.Stages.Select(s => new
        {
            Id = s.Id,
            Title = s.Title,
            recipes = s.RecipeIds.Select(id =>
            {
                var recipe = GetRecipe(id);
                return new
                {
                    recipe.Id,
                    recipe.Summary,
                    recipe.Menu,
                    recipe.HotkeyHint,
                    recipe.DesktopTools,
                    recipe.Params,
                    recipe.Verify
                };
            }).ToList()
        }).ToList();

        return new
        {
            playbookId = playbook.Id,
            playbookTitle = playbook.Title,
            notes = playbook.Notes,
            program,
            strategy = "sketch_then_dimension",
            worldHint =
                "Use desktop_drag for wall topology only. Enter exact lengths via CA dimensions after the shell closes. " +
                "Build Foundation -> slab AFTER floor-1 exterior walls - do not freehand-draw a slab from 0,0.",
            stages
        };
    }
}

/// <summary>In-memory CA session: Start, NextStep, MarkRecipeDone, blockers, Snapshot.</summary>
public sealed class CaSessionState
{
    private readonly object _gate = new();
    private string? _sessionId;
    private CaProgram? _program;
    private List<string> _stageIds = new();
    private int _stageIndex;
    private readonly HashSet<string> _completedRecipes = new(StringComparer.Ordinal);
    private readonly List<string> _blockers = new();

    public string Start(CaProgram program, CaPlaybookDoc playbook)
    {
        lock (_gate)
        {
            _sessionId = "ca-" + Guid.NewGuid().ToString("N")[..12];
            _program = program;
            _stageIds = playbook.Stages.Select(s => s.Id).ToList();
            _stageIndex = 0;
            _completedRecipes.Clear();
            _blockers.Clear();
            return _sessionId;
        }
    }

    public object NextStep(CaPlaybookLibrary lib)
    {
        lock (_gate)
        {
            if (_sessionId == null || _program == null)
            {
                return new
                {
                    error = "no active CA session - call ca_plan_project / ca_compile_brief first"
                };
            }

            var playbook = lib.GetResidentialSlabPlaybook();
            while (_stageIndex < _stageIds.Count)
            {
                var stageId = _stageIds[_stageIndex];
                var stage = playbook.Stages.First(s => s.Id == stageId);
                var nextRecipeId = stage.RecipeIds.FirstOrDefault(id => !_completedRecipes.Contains(id));
                if (nextRecipeId == null)
                {
                    _stageIndex++;
                    continue;
                }

                var recipe = lib.GetRecipe(nextRecipeId);
                return new
                {
                    sessionId = _sessionId,
                    done = false,
                    stageId = stage.Id,
                    stageTitle = stage.Title,
                    recipe,
                    program = _program,
                    blockers = _blockers.ToList(),
                    executeHint =
                        "Focus Chief Architect, follow recipe.menu / hotkey_hint, then use recipe.desktop_tools " +
                        "(desktop_drag for walls). Screenshot and call ca_verify_checklist before advancing."
                };
            }

            return new
            {
                sessionId = _sessionId,
                done = true,
                message = "All stages complete - run verify screenshots.",
                completedRecipes = _completedRecipes.ToList()
            };
        }
    }

    public void MarkRecipeDone(string recipeId)
    {
        lock (_gate)
        {
            _completedRecipes.Add(recipeId);
        }
    }

    public void AddBlocker(string message)
    {
        lock (_gate)
        {
            if (!string.IsNullOrWhiteSpace(message))
                _blockers.Add(message.Trim());
        }
    }

    public object Snapshot()
    {
        lock (_gate)
        {
            return new
            {
                sessionId = _sessionId,
                program = _program,
                stageIndex = _stageIndex,
                stageIds = _stageIds,
                completedRecipes = _completedRecipes.ToList(),
                blockers = _blockers.ToList()
            };
        }
    }
}
