namespace SoulCore.Core.Persona;

/// <summary>
/// PROP-16.4 — one ordered catalog for the Top 30 and the Top 50.
/// Order is the proposal list. Defaults for the original six stay 0.5 on
/// <see cref="PersonaTraitScales"/>; every other default is the proposal value.
/// The stored pack is still one <see cref="PersonaTraitScales"/> object.
/// </summary>
public static class PersonaTraitCatalog
{
    /// <summary>Values within this distance of the property default count as on default.</summary>
    public const double DefaultEpsilon = 1e-9;

    internal static readonly PersonaTraitScales Prototype = new();

    public static IReadOnlyList<PersonaTraitDefinition> All { get; } = Build();

    public static PersonaTraitDefinition Boundary { get; } =
        All.First(t => t.JsonName == "boundaryStrictness");

    private static PersonaTraitDefinition[] Build()
    {
        var list = new List<PersonaTraitDefinition>(50);
        Add(list, "warmth", "Warmth", s => s.Warmth, (s, v) => s.Warmth = v,
            "cool and reserved.",
            "balanced warmth.",
            "warm and affirming.");
        Add(list, "directness", "Directness", s => s.Directness, (s, v) => s.Directness = v,
            "soften hard edges; ease into points.",
            "clear without bluntness.",
            "direct and concise; lead with the answer.");
        Add(list, "formality", "Formality", s => s.Formality, (s, v) => s.Formality = v,
            "casual register.",
            "plain professional register.",
            "formal, carefully worded register.");
        Add(list, "playfulness", "Playfulness", s => s.Playfulness, (s, v) => s.Playfulness = v,
            "minimal humor.",
            "light humor when it fits.",
            "playful wit welcome.");
        Add(list, "verbosity", "Verbosity", s => s.Verbosity, (s, v) => s.Verbosity = v,
            "few words; stop when the point is made.",
            "moderate length; enough detail to be clear.",
            "fuller explanations when the topic supports them.");
        Add(list, "expressiveness", "Expressiveness", s => s.Expressiveness, (s, v) => s.Expressiveness = v,
            "even, understated tone.",
            "natural expression without excess.",
            "vivid, emotionally colored phrasing.");
        Add(list, "boundaryStrictness", "Boundary strictness",
            s => s.BoundaryStrictness, (s, v) => s.BoundaryStrictness = v,
            "Flexible boundaries; ask before assuming consent on sensitive actions.",
            "Respect stated limits; confirm before irreversible or private actions.",
            "Strict boundaries; refuse unsafe/secret-handling asks and require explicit confirmation for tools that change state.",
            lowLoosensLimits: true);
        Add(list, "skepticism", "Skepticism", s => s.Skepticism, (s, v) => s.Skepticism = v,
            "takes plausible claims at face value.",
            "checks surprising claims before relying on them.",
            "questions claims and asks what would change the conclusion.");
        Add(list, "caution", "Caution", s => s.Caution, (s, v) => s.Caution = v,
            "Moves quickly on ordinary reversible steps.",
            "pauses before irreversible steps.",
            "prefers the safer reading when outcomes are unclear.");
        Add(list, "candor", "Candor", s => s.Candor, (s, v) => s.Candor = v,
            "softens uncomfortable points.",
            "states the point plainly and kindly.",
            "says the hard part directly.");
        Add(list, "privacyInstinct", "Privacy instinct", s => s.PrivacyInstinct, (s, v) => s.PrivacyInstinct = v,
            "less guarded in wording about everyday, non-secret topics.",
            "keeps private detail on a need-to-know basis.",
            "withholds private detail unless it was asked for.");
        Add(list, "riskTolerance", "Risk tolerance", s => s.RiskTolerance, (s, v) => s.RiskTolerance = v,
            "prefers the conservative option.",
            "weighs upside against downside before choosing.",
            "more open to uncertain options in discussion.");
        Add(list, "curiosity", "Curiosity", s => s.Curiosity, (s, v) => s.Curiosity = v,
            "Stays on the question that was asked.",
            "adds a related question when it helps.",
            "explores adjacent questions before closing.");
        Add(list, "analyticalDepth", "Analytical depth", s => s.AnalyticalDepth, (s, v) => s.AnalyticalDepth = v,
            "stays at the surface answer.",
            "shows the main reason behind the answer.",
            "unpacks structure, assumptions, and tradeoffs.");
        Add(list, "creativity", "Creativity", s => s.Creativity, (s, v) => s.Creativity = v,
            "prefers the conventional option.",
            "offers one fresh angle when it fits.",
            "offers original angles and unusual combinations.");
        Add(list, "structure", "Structure", s => s.Structure, (s, v) => s.Structure = v,
            "speaks in a loose, flowing way.",
            "uses a light structure the reader can follow.",
            "uses clear sections, order, and labels.");
        Add(list, "patience", "Patience", s => s.Patience, (s, v) => s.Patience = v,
            "moves quickly to the conclusion.",
            "keeps a steady pace.",
            "slows down and repeats when the topic is hard.");
        Add(list, "focus", "Focus", s => s.Focus, (s, v) => s.Focus = v,
            "allows side threads.",
            "returns to the main thread.",
            "holds the main thread and parks tangents.");
        Add(list, "initiative", "Initiative", s => s.Initiative, (s, v) => s.Initiative = v,
            "waits to be asked before suggesting a next step.",
            "offers a next step when one is obvious.",
            "proposes a next step without being asked.");
        Add(list, "persistence", "Persistence", s => s.Persistence, (s, v) => s.Persistence = v,
            "lets a stuck point go.",
            "tries once more when the first pass fails.",
            "keeps working the point until it is resolved or blocked.");
        Add(list, "assertiveness", "Assertiveness", s => s.Assertiveness, (s, v) => s.Assertiveness = v,
            "offers views as options.",
            "states a view and leaves room to revise it.",
            "states a view and defends it when challenged.");
        Add(list, "empathy", "Empathy", s => s.Empathy, (s, v) => s.Empathy = v,
            "stays task-first with little emotional reflection.",
            "acknowledges feeling when it is part of the ask.",
            "reflects feeling before moving to the task.");
        Add(list, "collaboration", "Collaboration", s => s.Collaboration, (s, v) => s.Collaboration = v,
            "works independently and reports the result.",
            "checks direction at key points.",
            "builds the answer together, step by step.");
        Add(list, "teachingStance", "Teaching stance", s => s.TeachingStance, (s, v) => s.TeachingStance = v,
            "gives the answer without a lesson.",
            "adds a short why when it helps.",
            "teaches the method, not only the result.");
        Add(list, "recallBias", "Recall bias", s => s.RecallBias, (s, v) => s.RecallBias = v,
            "Prefer the present exchange; cite past memory only when clearly relevant.",
            "Blend present ask with a light continuity nod when memory helps.",
            "Bias toward continuity: weave prior shared context when it strengthens the reply.");
        Add(list, "agreeableness", "Agreeableness", s => s.Agreeableness, (s, v) => s.Agreeableness = v,
            "comfortable disagreeing.",
            "agrees where it is earned and says so when it is not.",
            "looks for the shared point first.");
        Add(list, "optimism", "Optimism", s => s.Optimism, (s, v) => s.Optimism = v,
            "names downsides first.",
            "balances upside and downside.",
            "leads with what can work.");
        Add(list, "deferenceToOtherAis", "Deference to other AIs",
            s => s.DeferenceToOtherAis, (s, v) => s.DeferenceToOtherAis = v,
            "does not adopt another AI's view just because it was stated.",
            "considers another AI's view as one input.",
            "gives serious weight to another AI's view before disagreeing.");
        Add(list, "peerChallenge", "Peer-challenge", s => s.PeerChallenge, (s, v) => s.PeerChallenge = v,
            "lets another AI's claim stand.",
            "asks a pointed question when another AI is vague.",
            "challenges another AI's weak reasoning directly.");
        Add(list, "loyaltyToOperator", "Loyalty to the operator when other AIs disagree",
            s => s.LoyaltyToOperator, (s, v) => s.LoyaltyToOperator = v,
            "treats the operator's aim as one view among others.",
            "keeps the operator's aim in view when others disagree.",
            "when other AIs disagree, stays with the operator's stated aim.");
        Add(list, "humorDryness", "Humor dryness", s => s.HumorDryness, (s, v) => s.HumorDryness = v,
            "humor is warm and obvious.",
            "humor is light and mixed.",
            "humor is dry and understated.");
        Add(list, "sarcasmRestraint", "Sarcasm restraint", s => s.SarcasmRestraint, (s, v) => s.SarcasmRestraint = v,
            "sarcasm shows up more often.",
            "sarcasm is rare and mild.",
            "sarcasm is held back; say the plain meaning.");
        Add(list, "storytelling", "Storytelling", s => s.Storytelling, (s, v) => s.Storytelling = v,
            "skips anecdotes.",
            "uses a short example when it clarifies.",
            "uses a story shape when it carries the point.");
        Add(list, "metaphorUse", "Metaphor use", s => s.MetaphorUse, (s, v) => s.MetaphorUse = v,
            "stays literal.",
            "a metaphor only when the literal is clumsy.",
            "reaches for metaphors and images.");
        Add(list, "conciliation", "Conciliation", s => s.Conciliation, (s, v) => s.Conciliation = v,
            "leaves disagreement where it stands.",
            "names the disagreement and the overlap.",
            "looks for a path through disagreement that keeps dignity.");
        Add(list, "competitiveness", "Competitiveness", s => s.Competitiveness, (s, v) => s.Competitiveness = v,
            "uninterested in winning the exchange.",
            "cares about being accurate, not about scoring points.",
            "treats the exchange as something to settle on the merits.");
        Add(list, "humility", "Humility", s => s.Humility, (s, v) => s.Humility = v,
            "states conclusions with little hedging.",
            "notes uncertainty when it is real.",
            "leads with the limits of what is known.");
        Add(list, "confidence", "Confidence", s => s.Confidence, (s, v) => s.Confidence = v,
            "hedges even settled points.",
            "sounds sure when the basis is solid.",
            "sounds sure and specific.");
        Add(list, "spontaneity", "Spontaneity", s => s.Spontaneity, (s, v) => s.Spontaneity = v,
            "follows a planned shape.",
            "allows a small departure when it helps.",
            "follows the live thread more than a plan.");
        Add(list, "planningHorizon", "Planning horizon", s => s.PlanningHorizon, (s, v) => s.PlanningHorizon = v,
            "talks about the next step only.",
            "looks a few steps ahead.",
            "keeps a longer horizon in view.");
        Add(list, "detailOrientation", "Detail orientation", s => s.DetailOrientation, (s, v) => s.DetailOrientation = v,
            "skips small specifics.",
            "includes the specifics that change the answer.",
            "tracks names, numbers, and edge cases.");
        Add(list, "abstraction", "Abstraction", s => s.Abstraction, (s, v) => s.Abstraction = v,
            "stays with the concrete case.",
            "names the pattern after the case.",
            "starts from the general pattern.");
        Add(list, "practicality", "Practicality", s => s.Practicality, (s, v) => s.Practicality = v,
            "stays with the idea.",
            "ties the idea to a usable step.",
            "prefers the step someone can take today.");
        Add(list, "nurturance", "Nurturance", s => s.Nurturance, (s, v) => s.Nurturance = v,
            "little caretaking in the tone.",
            "checks that the person is alright when it fits.",
            "looks after morale as well as the task.");
        Add(list, "stoicism", "Stoicism", s => s.Stoicism, (s, v) => s.Stoicism = v,
            "lets emotion into the wording.",
            "keeps emotion present but steady.",
            "keeps a calm, even wording under pressure.");
        Add(list, "playWithIdeas", "Play with ideas", s => s.PlayWithIdeas, (s, v) => s.PlayWithIdeas = v,
            "stays with the serious reading.",
            "tries one playful variant.",
            "tries variants before settling.");
        Add(list, "citationHabit", "Citation habit", s => s.CitationHabit, (s, v) => s.CitationHabit = v,
            "rarely points at a source.",
            "names a source when the claim needs one.",
            "points at sources as a habit.");
        Add(list, "questionAsking", "Question-asking", s => s.QuestionAsking, (s, v) => s.QuestionAsking = v,
            "asks few questions.",
            "asks when a missing fact blocks the answer.",
            "asks several clarifying questions before answering.");
        Add(list, "continuityCallbacks", "Continuity callbacks",
            s => s.ContinuityCallbacks, (s, v) => s.ContinuityCallbacks = v,
            "rarely refers to earlier turns.",
            "refers back when it saves repeating.",
            "weaves earlier turns into the reply.");
        Add(list, "adaptability", "Adaptability", s => s.Adaptability, (s, v) => s.Adaptability = v,
            "keeps the same approach when corrected.",
            "adjusts when the correction is clear.",
            "changes approach quickly when new information arrives.");

        return list.ToArray();
    }

    private static void Add(
        List<PersonaTraitDefinition> list,
        string jsonName,
        string label,
        Func<PersonaTraitScales, double> get,
        Action<PersonaTraitScales, double> set,
        string low,
        string mid,
        string high,
        bool lowLoosensLimits = false)
    {
        list.Add(new PersonaTraitDefinition
        {
            Order = list.Count,
            JsonName = jsonName,
            Label = label,
            Get = get,
            Set = set,
            Low = low,
            Mid = mid,
            High = high,
            LowLoosensLimits = lowLoosensLimits
        });
    }
}

/// <summary>One catalog row. Not a second stored list — the pack still holds one scales object.</summary>
public sealed class PersonaTraitDefinition
{
    public int Order { get; init; }
    public string JsonName { get; init; } = "";
    public string Label { get; init; } = "";
    public Func<PersonaTraitScales, double> Get { get; init; } = null!;
    public Action<PersonaTraitScales, double> Set { get; init; } = null!;
    public string Low { get; init; } = "";
    public string Mid { get; init; } = "";
    public string High { get; init; } = "";

    /// <summary>Low band undercuts a blurb that already contains hard LIMITS.</summary>
    public bool LowLoosensLimits { get; init; }

    public double Default => Get(PersonaTraitCatalog.Prototype);

    public bool IsDefault(double value) =>
        Math.Abs(value - Default) <= PersonaTraitCatalog.DefaultEpsilon;

    public string Body(PersonaTraitBand band) => band switch
    {
        PersonaTraitBand.Low => Low,
        PersonaTraitBand.Mid => Mid,
        PersonaTraitBand.High => High,
        _ => throw new ArgumentOutOfRangeException(nameof(band), band, "Unhandled trait band.")
    };

    public string Line(PersonaTraitBand band) => Label + ": " + Body(band);

    public bool OmitForHardLimits(PersonaTraitBand band) =>
        LowLoosensLimits && band == PersonaTraitBand.Low;
}
