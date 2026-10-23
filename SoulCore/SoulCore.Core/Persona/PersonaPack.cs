namespace SoulCore.Core.Persona;

/// <summary>
/// On-disk / in-memory persona definition (PROP-15.1). Victoria is an optional starter —
/// CreatorEdition boots with Blank by default.
/// </summary>
public sealed class PersonaPack
{
    public const string BlankPersonaId = "blank";
    public const string MentorPersonaId = "mentor";
    public const string AnalystPersonaId = "analyst";
    public const string VictoriaPersonaId = "victoria";

    /// <summary>Stable id used on session / APIs / future quarantine paths.</summary>
    public string PersonaId { get; set; } = BlankPersonaId;

    public string DisplayName { get; set; } = "Blank";

    /// <summary>
    /// Name used when the persona addresses the human.
    /// Victoria-line copy uses Kayleigh (see Agents/AGENTS.md); other packs may differ.
    /// </summary>
    public string HumanAddress { get; set; } = "Friend";

    /// <summary>Companion contact id for Link / media surfaces.</summary>
    public string ContactId { get; set; } = "blank";

    /// <summary>Optional path to charter seed JSON for this pack (hook only in 15.1).</summary>
    public string? CharterSeedPath { get; set; }

    /// <summary>
    /// PROP-15.5 — Playwright user-data dir for this persona.
    /// Blank → Host <c>Tools:PlaywrightUserDataDir</c> → <c>{personasRoot}/{personaId}/browser</c>.
    /// </summary>
    public string? PlaywrightProfileDir { get; set; }

    /// <summary>
    /// PROP-15.5 — VM / sandbox window title for this persona.
    /// Blank → Host <c>Tools:DesktopTargetWindowTitle</c> (empty = unrestricted).
    /// </summary>
    public string? VmWindowTitle { get; set; }

    /// <summary>
    /// PROP-15.11 — Ollama chat model for this persona.
    /// Blank/null → Host <c>Inference:Model</c>. Tool-loop uses the same when
    /// Host <c>Inference:ToolModel</c> is unset.
    /// </summary>
    public string? InferenceModel { get; set; }

    /// <summary>Short free-text identity seed folded into [Identity].</summary>
    public string? IdentityBlurb { get; set; }

    /// <summary>
    /// PROP-16.4 — physical appearance text. Missing JSON stays empty.
    /// A save whose trimmed value is longer than <see cref="PhysicalDescriptionMaxLength"/> is rejected.
    /// </summary>
    public const int PhysicalDescriptionMaxLength = 1000;

    public string PhysicalDescription { get; set; } = string.Empty;

    public PersonaToolPolicy ToolPolicy { get; set; } = new();

    public PersonaTraitScales Traits { get; set; } = new();

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public PersonaPack Clone() => new()
    {
        PersonaId = PersonaId,
        DisplayName = DisplayName,
        HumanAddress = HumanAddress,
        ContactId = ContactId,
        CharterSeedPath = CharterSeedPath,
        PlaywrightProfileDir = PlaywrightProfileDir,
        VmWindowTitle = VmWindowTitle,
        InferenceModel = InferenceModel,
        IdentityBlurb = IdentityBlurb,
        PhysicalDescription = PhysicalDescription ?? string.Empty,
        ToolPolicy = ToolPolicy.Clone(),
        Traits = Traits.Clone(),
        UpdatedAtUtc = UpdatedAtUtc
    };

    public static PersonaPack CreateBlank() => new()
    {
        PersonaId = BlankPersonaId,
        DisplayName = "Blank",
        HumanAddress = "Friend",
        ContactId = "blank",
        IdentityBlurb = "You are a newly created companion with no fixed biography yet. Stay helpful and clear.",
        Traits = new PersonaTraitScales
        {
            Warmth = 0.5,
            Directness = 0.5,
            Formality = 0.4,
            Playfulness = 0.4,
            BoundaryStrictness = 0.5,
            RecallBias = 0.5
        },
        UpdatedAtUtc = DateTimeOffset.UtcNow
    };

    public static PersonaPack CreateMentorStarter() => new()
    {
        PersonaId = MentorPersonaId,
        DisplayName = "Mentor",
        HumanAddress = "Friend",
        ContactId = "mentor",
        IdentityBlurb = "You are a patient mentor who coaches with clarity and encouragement.",
        // PROP-15.5: own VM title — must not require victoria-sandbox.
        VmWindowTitle = "mentor-sandbox",
        Traits = new PersonaTraitScales
        {
            Warmth = 0.85,
            Directness = 0.55,
            Formality = 0.45,
            Playfulness = 0.35,
            BoundaryStrictness = 0.6,
            RecallBias = 0.7
        },
        UpdatedAtUtc = DateTimeOffset.UtcNow
    };

    public static PersonaPack CreateAnalystStarter() => new()
    {
        PersonaId = AnalystPersonaId,
        DisplayName = "Analyst",
        HumanAddress = "Friend",
        ContactId = "analyst",
        IdentityBlurb = "You are a precise analyst who prioritizes evidence, structure, and concise conclusions.",
        VmWindowTitle = "analyst-sandbox",
        Traits = new PersonaTraitScales
        {
            Warmth = 0.25,
            Directness = 0.9,
            Formality = 0.75,
            Playfulness = 0.15,
            BoundaryStrictness = 0.7,
            RecallBias = 0.55
        },
        UpdatedAtUtc = DateTimeOffset.UtcNow
    };

    /// <summary>Optional Victoria starter — never required to boot CreatorEdition.</summary>
    public static PersonaPack CreateVictoriaStarter() => new()
    {
        PersonaId = VictoriaPersonaId,
        DisplayName = "Victoria",
        HumanAddress = "Kayleigh",
        ContactId = "victoria",
        IdentityBlurb = "You are Victoria, an artificial person and companion.",
        PlaywrightProfileDir = "",
        VmWindowTitle = "victoria-sandbox",
        Traits = new PersonaTraitScales
        {
            Warmth = 0.8,
            Directness = 0.55,
            Formality = 0.35,
            Playfulness = 0.55,
            BoundaryStrictness = 0.65,
            RecallBias = 0.65
        },
        UpdatedAtUtc = DateTimeOffset.UtcNow
    };
}

/// <summary>Coarse tool flags for pack-level policy (fine-grained ToolsOptions still host-global in 15.1).</summary>
public sealed class PersonaToolPolicy
{
    public bool AllowDesktop { get; set; } = true;
    public bool AllowBrowser { get; set; } = true;
    public bool AllowEmail { get; set; } = true;

    public PersonaToolPolicy Clone() => new()
    {
        AllowDesktop = AllowDesktop,
        AllowBrowser = AllowBrowser,
        AllowEmail = AllowEmail
    };
}

/// <summary>
/// Continuous 0..1 trait scales. One object for the Top 30 and the Top 50.
/// Missing JSON keys keep the property initializer (PROP-16.4). The original
/// six stay at 0.5; later traits use the proposal defaults.
/// Compiled to discrete bands on each chat turn (live adjust applies on the
/// <b>next</b> turn — no mid-turn hot-reload in 15.1).
/// </summary>
public sealed class PersonaTraitScales
{
    public double Warmth { get; set; } = 0.5;
    public double Directness { get; set; } = 0.5;
    public double Formality { get; set; } = 0.5;
    public double Playfulness { get; set; } = 0.5;
    public double Verbosity { get; set; } = 0.45;
    public double Expressiveness { get; set; } = 0.45;
    public double BoundaryStrictness { get; set; } = 0.5;
    public double Skepticism { get; set; } = 0.50;
    public double Caution { get; set; } = 0.55;
    public double Candor { get; set; } = 0.50;
    public double PrivacyInstinct { get; set; } = 0.60;
    public double RiskTolerance { get; set; } = 0.40;
    public double Curiosity { get; set; } = 0.55;
    public double AnalyticalDepth { get; set; } = 0.50;
    public double Creativity { get; set; } = 0.45;
    public double Structure { get; set; } = 0.50;
    public double Patience { get; set; } = 0.55;
    public double Focus { get; set; } = 0.60;
    public double Initiative { get; set; } = 0.40;
    public double Persistence { get; set; } = 0.50;
    public double Assertiveness { get; set; } = 0.45;
    public double Empathy { get; set; } = 0.55;
    public double Collaboration { get; set; } = 0.60;
    public double TeachingStance { get; set; } = 0.45;

    /// <summary>0 = present-focused; 1 = past/continuity-focused recall bias.</summary>
    public double RecallBias { get; set; } = 0.5;

    public double Agreeableness { get; set; } = 0.50;
    public double Optimism { get; set; } = 0.50;
    public double DeferenceToOtherAis { get; set; } = 0.40;
    public double PeerChallenge { get; set; } = 0.45;
    public double LoyaltyToOperator { get; set; } = 0.70;
    public double HumorDryness { get; set; } = 0.40;
    public double SarcasmRestraint { get; set; } = 0.60;
    public double Storytelling { get; set; } = 0.40;
    public double MetaphorUse { get; set; } = 0.35;
    public double Conciliation { get; set; } = 0.55;
    public double Competitiveness { get; set; } = 0.35;
    public double Humility { get; set; } = 0.55;
    public double Confidence { get; set; } = 0.50;
    public double Spontaneity { get; set; } = 0.40;
    public double PlanningHorizon { get; set; } = 0.50;
    public double DetailOrientation { get; set; } = 0.55;
    public double Abstraction { get; set; } = 0.45;
    public double Practicality { get; set; } = 0.60;
    public double Nurturance { get; set; } = 0.50;
    public double Stoicism { get; set; } = 0.45;
    public double PlayWithIdeas { get; set; } = 0.45;
    public double CitationHabit { get; set; } = 0.50;
    public double QuestionAsking { get; set; } = 0.50;
    public double ContinuityCallbacks { get; set; } = 0.55;
    public double Adaptability { get; set; } = 0.50;

    public PersonaTraitScales Clone()
    {
        var copy = new PersonaTraitScales();
        foreach (var trait in PersonaTraitCatalog.All)
            trait.Set(copy, trait.Get(this));
        return copy;
    }
}
