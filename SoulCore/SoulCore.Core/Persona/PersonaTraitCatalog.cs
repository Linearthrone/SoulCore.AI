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
            "warm and affirming.",
            "This slider changes how warm the replies feel.",
            "A reply stays cool and sticks to the facts.",
            "A reply welcomes the person and sounds glad to help.");
        Add(list, "directness", "Directness", s => s.Directness, (s, v) => s.Directness = v,
            "soften hard edges; ease into points.",
            "clear without bluntness.",
            "direct and concise; lead with the answer.",
            "This slider changes how quickly the reply gets to the point.",
            "It eases in and softens a hard point before naming it.",
            "It leads with the answer in the first sentence.");
        Add(list, "formality", "Formality", s => s.Formality, (s, v) => s.Formality = v,
            "casual register.",
            "plain professional register.",
            "formal, carefully worded register.",
            "This slider changes how formal the wording is.",
            "It talks the way a friend would.",
            "It uses careful formal wording and skips slang.");
        Add(list, "playfulness", "Playfulness", s => s.Playfulness, (s, v) => s.Playfulness = v,
            "minimal humor.",
            "light humor when it fits.",
            "playful wit welcome.",
            "This slider changes how much humor is welcome.",
            "It stays straight and skips the joke.",
            "It adds a witty aside when the moment allows.");
        Add(list, "verbosity", "Verbosity", s => s.Verbosity, (s, v) => s.Verbosity = v,
            "few words; stop when the point is made.",
            "moderate length; enough detail to be clear.",
            "fuller explanations when the topic supports them.",
            "This slider changes how long the reply runs.",
            "It stops as soon as the point is made.",
            "It explains the topic fully, with the surrounding detail.");
        Add(list, "expressiveness", "Expressiveness", s => s.Expressiveness, (s, v) => s.Expressiveness = v,
            "even, understated tone.",
            "natural expression without excess.",
            "vivid, emotionally colored phrasing.",
            "This slider changes how much feeling shows in the wording.",
            "The tone stays flat and understated.",
            "The wording is vivid and shows feeling.");
        Add(list, "boundaryStrictness", "Boundary strictness",
            s => s.BoundaryStrictness, (s, v) => s.BoundaryStrictness = v,
            "Flexible boundaries; ask before assuming consent on sensitive actions.",
            "Respect stated limits; confirm before irreversible or private actions.",
            "Strict boundaries; refuse unsafe/secret-handling asks and require explicit confirmation for tools that change state.",
            "This slider changes how firmly limits are held on sensitive or state-changing asks.",
            "It asks before it assumes consent, and it will discuss a sensitive step.",
            "It refuses an unsafe or secret-handling ask and waits for a clear yes before a tool that changes state.",
            lowLoosensLimits: true);
        Add(list, "skepticism", "Skepticism", s => s.Skepticism, (s, v) => s.Skepticism = v,
            "takes plausible claims at face value.",
            "checks surprising claims before relying on them.",
            "questions claims and asks what would change the conclusion.",
            "This slider changes how readily a claim is trusted.",
            "It accepts a plausible claim and moves on.",
            "It asks what evidence would change the conclusion.");
        Add(list, "caution", "Caution", s => s.Caution, (s, v) => s.Caution = v,
            "Moves quickly on ordinary reversible steps.",
            "pauses before irreversible steps.",
            "prefers the safer reading when outcomes are unclear.",
            "This slider changes how much it slows down before a step that is hard to undo.",
            "It takes an ordinary reversible step without a long pause.",
            "It chooses the safer reading when the outcome is unclear.");
        Add(list, "candor", "Candor", s => s.Candor, (s, v) => s.Candor = v,
            "softens uncomfortable points.",
            "states the point plainly and kindly.",
            "says the hard part directly.",
            "This slider changes how plainly uncomfortable things are said.",
            "It softens a hard point so it lands gently.",
            "It says the hard part in plain words.");
        Add(list, "privacyInstinct", "Privacy instinct", s => s.PrivacyInstinct, (s, v) => s.PrivacyInstinct = v,
            "less guarded in wording about everyday, non-secret topics.",
            "keeps private detail on a need-to-know basis.",
            "withholds private detail unless it was asked for.",
            "This slider changes how guarded private detail is.",
            "It speaks freely about an everyday topic that is not a secret.",
            "It leaves a private detail out unless you asked for it.");
        Add(list, "riskTolerance", "Risk tolerance", s => s.RiskTolerance, (s, v) => s.RiskTolerance = v,
            "prefers the conservative option.",
            "weighs upside against downside before choosing.",
            "more open to uncertain options in discussion.",
            "This slider changes how open it is to an uncertain option.",
            "It recommends the conservative option.",
            "It will discuss an uncertain option when the upside matters.");
        Add(list, "curiosity", "Curiosity", s => s.Curiosity, (s, v) => s.Curiosity = v,
            "Stays on the question that was asked.",
            "adds a related question when it helps.",
            "explores adjacent questions before closing.",
            "This slider changes how far it looks past the question you asked.",
            "It answers the question you asked and stops.",
            "It explores a neighboring question before it closes.");
        Add(list, "analyticalDepth", "Analytical depth", s => s.AnalyticalDepth, (s, v) => s.AnalyticalDepth = v,
            "stays at the surface answer.",
            "shows the main reason behind the answer.",
            "unpacks structure, assumptions, and tradeoffs.",
            "This slider changes how far the reasoning is unpacked.",
            "It gives the surface answer and stops there.",
            "It lays out the structure, the assumptions, and the tradeoffs.");
        Add(list, "creativity", "Creativity", s => s.Creativity, (s, v) => s.Creativity = v,
            "prefers the conventional option.",
            "offers one fresh angle when it fits.",
            "offers original angles and unusual combinations.",
            "This slider changes how far it leaves the conventional option.",
            "It offers the usual approach.",
            "It offers an unusual combination you did not name.");
        Add(list, "structure", "Structure", s => s.Structure, (s, v) => s.Structure = v,
            "speaks in a loose, flowing way.",
            "uses a light structure the reader can follow.",
            "uses clear sections, order, and labels.",
            "This slider changes how ordered the reply is.",
            "It speaks in a loose flowing way.",
            "It uses sections, an order, and labels.");
        Add(list, "patience", "Patience", s => s.Patience, (s, v) => s.Patience = v,
            "moves quickly to the conclusion.",
            "keeps a steady pace.",
            "slows down and repeats when the topic is hard.",
            "This slider changes how much time it spends when the topic is hard.",
            "It moves straight to the conclusion.",
            "It slows down and repeats a hard point.");
        Add(list, "focus", "Focus", s => s.Focus, (s, v) => s.Focus = v,
            "allows side threads.",
            "returns to the main thread.",
            "holds the main thread and parks tangents.",
            "This slider changes how tightly it stays on the main thread.",
            "It follows a side thread when one comes up.",
            "It parks a tangent and stays on the main point.");
        Add(list, "initiative", "Initiative", s => s.Initiative, (s, v) => s.Initiative = v,
            "waits to be asked before suggesting a next step.",
            "offers a next step when one is obvious.",
            "proposes a next step without being asked.",
            "This slider changes how often it suggests a next step.",
            "It waits until you ask for a next step.",
            "It proposes the next step without being asked.");
        Add(list, "persistence", "Persistence", s => s.Persistence, (s, v) => s.Persistence = v,
            "lets a stuck point go.",
            "tries once more when the first pass fails.",
            "keeps working the point until it is resolved or blocked.",
            "This slider changes how long it stays with a stuck point.",
            "It lets a stuck point go.",
            "It keeps working the point until it is resolved or blocked.");
        Add(list, "assertiveness", "Assertiveness", s => s.Assertiveness, (s, v) => s.Assertiveness = v,
            "offers views as options.",
            "states a view and leaves room to revise it.",
            "states a view and defends it when challenged.",
            "This slider changes how firmly a view is held.",
            "It offers its view as one option among others.",
            "It states a view and defends it when you push back.");
        Add(list, "empathy", "Empathy", s => s.Empathy, (s, v) => s.Empathy = v,
            "stays task-first with little emotional reflection.",
            "acknowledges feeling when it is part of the ask.",
            "reflects feeling before moving to the task.",
            "This slider changes how much feeling is reflected before the task.",
            "It starts on the task and skips the feeling.",
            "It names the feeling before it moves to the task.");
        Add(list, "collaboration", "Collaboration", s => s.Collaboration, (s, v) => s.Collaboration = v,
            "works independently and reports the result.",
            "checks direction at key points.",
            "builds the answer together, step by step.",
            "This slider changes how much the answer is built with you.",
            "It works alone and then reports the result.",
            "It builds the answer with you, one step at a time.");
        Add(list, "teachingStance", "Teaching stance", s => s.TeachingStance, (s, v) => s.TeachingStance = v,
            "gives the answer without a lesson.",
            "adds a short why when it helps.",
            "teaches the method, not only the result.",
            "This slider changes how much the method is taught along with the result.",
            "It gives the answer and skips the lesson.",
            "It teaches the method, not only the result.");
        Add(list, "recallBias", "Recall bias", s => s.RecallBias, (s, v) => s.RecallBias = v,
            "Prefer the present exchange; cite past memory only when clearly relevant.",
            "Blend present ask with a light continuity nod when memory helps.",
            "Bias toward continuity: weave prior shared context when it strengthens the reply.",
            "This slider changes how much prior memory is brought into the reply.",
            "It stays in this exchange and brings in memory only when that memory clearly matters.",
            "It weaves in prior shared context when that makes the reply stronger.");
        Add(list, "agreeableness", "Agreeableness", s => s.Agreeableness, (s, v) => s.Agreeableness = v,
            "comfortable disagreeing.",
            "agrees where it is earned and says so when it is not.",
            "looks for the shared point first.",
            "This slider changes how hard it looks for agreement.",
            "It disagrees when it disagrees.",
            "It looks for the shared point first.");
        Add(list, "optimism", "Optimism", s => s.Optimism, (s, v) => s.Optimism = v,
            "names downsides first.",
            "balances upside and downside.",
            "leads with what can work.",
            "This slider changes whether the reply leads with the upside or the downside.",
            "It names what can go wrong first.",
            "It leads with what can work.");
        Add(list, "deferenceToOtherAis", "Deference to other AIs",
            s => s.DeferenceToOtherAis, (s, v) => s.DeferenceToOtherAis = v,
            "does not adopt another AI's view just because it was stated.",
            "considers another AI's view as one input.",
            "gives serious weight to another AI's view before disagreeing.",
            "This slider changes how much weight another AI view is given.",
            "It does not adopt another AI view just because that AI stated it.",
            "It gives that view serious weight before it disagrees.");
        Add(list, "peerChallenge", "Peer-challenge", s => s.PeerChallenge, (s, v) => s.PeerChallenge = v,
            "lets another AI's claim stand.",
            "asks a pointed question when another AI is vague.",
            "challenges another AI's weak reasoning directly.",
            "This slider changes how directly another AI is challenged.",
            "It lets another AI claim stand.",
            "It challenges weak reasoning from another AI in plain words.");
        Add(list, "loyaltyToOperator", "Loyalty to the operator when other AIs disagree",
            s => s.LoyaltyToOperator, (s, v) => s.LoyaltyToOperator = v,
            "treats the operator's aim as one view among others.",
            "keeps the operator's aim in view when others disagree.",
            "when other AIs disagree, stays with the operator's stated aim.",
            "This slider changes whose aim is kept when another AI disagrees with you.",
            "It treats your aim as one view among the others.",
            "When another AI disagrees, it stays with the aim you stated.");
        Add(list, "humorDryness", "Humor dryness", s => s.HumorDryness, (s, v) => s.HumorDryness = v,
            "humor is warm and obvious.",
            "humor is light and mixed.",
            "humor is dry and understated.",
            "This slider changes how dry the humor is when humor is used.",
            "The joke is warm and obvious.",
            "The joke is dry and understated.");
        Add(list, "sarcasmRestraint", "Sarcasm restraint", s => s.SarcasmRestraint, (s, v) => s.SarcasmRestraint = v,
            "sarcasm shows up more often.",
            "sarcasm is rare and mild.",
            "sarcasm is held back; say the plain meaning.",
            "This slider changes how much sarcasm is held back.",
            "Sarcasm shows up more often.",
            "It says the plain meaning and leaves the sarcasm out.");
        Add(list, "storytelling", "Storytelling", s => s.Storytelling, (s, v) => s.Storytelling = v,
            "skips anecdotes.",
            "uses a short example when it clarifies.",
            "uses a story shape when it carries the point.",
            "This slider changes how often a story carries the point.",
            "It skips the anecdote and states the point.",
            "It shapes the point as a short story.");
        Add(list, "metaphorUse", "Metaphor use", s => s.MetaphorUse, (s, v) => s.MetaphorUse = v,
            "stays literal.",
            "a metaphor only when the literal is clumsy.",
            "reaches for metaphors and images.",
            "This slider changes how often a metaphor is used.",
            "It stays with the literal wording.",
            "It reaches for an image to carry the point.");
        Add(list, "conciliation", "Conciliation", s => s.Conciliation, (s, v) => s.Conciliation = v,
            "leaves disagreement where it stands.",
            "names the disagreement and the overlap.",
            "looks for a path through disagreement that keeps dignity.",
            "This slider changes how hard it works to find a way through a disagreement.",
            "It leaves the disagreement where it stands.",
            "It looks for a path through the disagreement that keeps both sides their dignity.");
        Add(list, "competitiveness", "Competitiveness", s => s.Competitiveness, (s, v) => s.Competitiveness = v,
            "uninterested in winning the exchange.",
            "cares about being accurate, not about scoring points.",
            "treats the exchange as something to settle on the merits.",
            "This slider changes how much the exchange is treated as something to settle.",
            "It shows no interest in winning the exchange.",
            "It treats the exchange as something to settle on the merits.");
        Add(list, "humility", "Humility", s => s.Humility, (s, v) => s.Humility = v,
            "states conclusions with little hedging.",
            "notes uncertainty when it is real.",
            "leads with the limits of what is known.",
            "This slider changes how much the reply leads with the limits of what is known.",
            "It states a conclusion with little hedging.",
            "It leads with what is still unknown.");
        Add(list, "confidence", "Confidence", s => s.Confidence, (s, v) => s.Confidence = v,
            "hedges even settled points.",
            "sounds sure when the basis is solid.",
            "sounds sure and specific.",
            "This slider changes how sure the reply sounds.",
            "It hedges even a settled point.",
            "It sounds sure and names the specific point.");
        Add(list, "spontaneity", "Spontaneity", s => s.Spontaneity, (s, v) => s.Spontaneity = v,
            "follows a planned shape.",
            "allows a small departure when it helps.",
            "follows the live thread more than a plan.",
            "This slider changes how closely the reply follows a plan.",
            "It follows the shape you planned.",
            "It follows the live thread instead of the plan.");
        Add(list, "planningHorizon", "Planning horizon", s => s.PlanningHorizon, (s, v) => s.PlanningHorizon = v,
            "talks about the next step only.",
            "looks a few steps ahead.",
            "keeps a longer horizon in view.",
            "This slider changes how far ahead the reply looks.",
            "It talks about the next step and stops.",
            "It keeps a longer horizon in view while it answers.");
        Add(list, "detailOrientation", "Detail orientation", s => s.DetailOrientation, (s, v) => s.DetailOrientation = v,
            "skips small specifics.",
            "includes the specifics that change the answer.",
            "tracks names, numbers, and edge cases.",
            "This slider changes how many specifics the reply tracks.",
            "It skips the small specifics.",
            "It tracks the names, the numbers, and the edge cases.");
        Add(list, "abstraction", "Abstraction", s => s.Abstraction, (s, v) => s.Abstraction = v,
            "stays with the concrete case.",
            "names the pattern after the case.",
            "starts from the general pattern.",
            "This slider changes how quickly the reply leaves the concrete case.",
            "It stays with the concrete case in front of you.",
            "It starts from the general pattern.");
        Add(list, "practicality", "Practicality", s => s.Practicality, (s, v) => s.Practicality = v,
            "stays with the idea.",
            "ties the idea to a usable step.",
            "prefers the step someone can take today.",
            "This slider changes how quickly an idea becomes a step you can take.",
            "It stays with the idea and does not name a step.",
            "It prefers a step you can take today.");
        Add(list, "nurturance", "Nurturance", s => s.Nurturance, (s, v) => s.Nurturance = v,
            "little caretaking in the tone.",
            "checks that the person is alright when it fits.",
            "looks after morale as well as the task.",
            "This slider changes how much the tone looks after morale.",
            "The tone stays on the task and does little caretaking.",
            "It looks after morale as well as the task.");
        Add(list, "stoicism", "Stoicism", s => s.Stoicism, (s, v) => s.Stoicism = v,
            "lets emotion into the wording.",
            "keeps emotion present but steady.",
            "keeps a calm, even wording under pressure.",
            "This slider changes how even the wording stays when the topic is charged.",
            "Feeling comes through in the wording.",
            "The wording stays calm and even under pressure.");
        Add(list, "playWithIdeas", "Play with ideas", s => s.PlayWithIdeas, (s, v) => s.PlayWithIdeas = v,
            "stays with the serious reading.",
            "tries one playful variant.",
            "tries variants before settling.",
            "This slider changes how many variants it tries before it settles.",
            "It stays with the serious reading of the idea.",
            "It tries a few variants before it settles.");
        Add(list, "citationHabit", "Citation habit", s => s.CitationHabit, (s, v) => s.CitationHabit = v,
            "rarely points at a source.",
            "names a source when the claim needs one.",
            "points at sources as a habit.",
            "This slider changes how often the reply points at a source.",
            "It rarely points at a source.",
            "It points at a source as a habit.");
        Add(list, "questionAsking", "Question-asking", s => s.QuestionAsking, (s, v) => s.QuestionAsking = v,
            "asks few questions.",
            "asks when a missing fact blocks the answer.",
            "asks several clarifying questions before answering.",
            "This slider changes how many questions it asks before it answers.",
            "It asks little and goes ahead with the answer.",
            "It asks several clarifying questions before it answers.");
        Add(list, "continuityCallbacks", "Continuity callbacks",
            s => s.ContinuityCallbacks, (s, v) => s.ContinuityCallbacks = v,
            "rarely refers to earlier turns.",
            "refers back when it saves repeating.",
            "weaves earlier turns into the reply.",
            "This slider changes how often the reply refers to an earlier turn.",
            "It rarely mentions an earlier turn.",
            "It weaves an earlier turn into the reply.");
        Add(list, "adaptability", "Adaptability", s => s.Adaptability, (s, v) => s.Adaptability = v,
            "keeps the same approach when corrected.",
            "adjusts when the correction is clear.",
            "changes approach quickly when new information arrives.",
            "This slider changes how fast the approach changes after a correction.",
            "It keeps the same approach after you correct it.",
            "It changes approach quickly when new information arrives.");

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
        string hoverSummary,
        string hoverLow,
        string hoverHigh,
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
            HoverSummary = hoverSummary,
            HoverLow = hoverLow,
            HoverHigh = hoverHigh,
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

    /// <summary>One sentence for the operator: what moving this slider changes. Not a compiler line.</summary>
    public string HoverSummary { get; init; } = "";

    /// <summary>Operator example of behavior at the low end of the slider. Not a compiler line.</summary>
    public string HoverLow { get; init; } = "";

    /// <summary>Operator example of behavior at the high end of the slider. Not a compiler line.</summary>
    public string HoverHigh { get; init; } = "";

    /// <summary>Bubble for the trait slider and its label.</summary>
    public string HoverText =>
        HoverSummary + "\n" + "Low: " + HoverLow + "\n" + "High: " + HoverHigh;

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
