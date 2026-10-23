using System.Text;

namespace SoulCore.Core.Persona;

/// <summary>Discrete trait band after snapping a 0..1 scale.</summary>
public enum PersonaTraitBand
{
    Low = 0,
    Mid = 1,
    High = 2
}

/// <summary>
/// Compiles continuous trait scales into at most eight directive lines.
/// Band edges are fixed so a slider that crosses 0.34 or 0.67 changes the line.
/// Traits sitting on their own property default stay out. If every trait is on
/// its default, the marker is omitted too.
/// </summary>
public static class PersonaTraitCompiler
{
    public const string Marker = "[Persona directives]";

    /// <summary>Directive lines after <see cref="Marker"/>. The marker does not count.</summary>
    public const int MaxDirectiveLines = 8;

    /// <summary>Low &lt; 0.34, Mid &lt; 0.67, else High.</summary>
    public static PersonaTraitBand ToBand(double scale)
    {
        var clamped = Math.Clamp(scale, 0.0, 1.0);
        if (clamped < 0.34)
            return PersonaTraitBand.Low;
        if (clamped < 0.67)
            return PersonaTraitBand.Mid;
        return PersonaTraitBand.High;
    }

    /// <summary>
    /// Compiles off-default traits into a directive block.
    /// Boundary strictness, when emitted, is line 1 and counts toward the eight.
    /// Other off-default traits follow by absolute distance from their own default,
    /// ties broken by proposal catalog order.
    /// When <paramref name="hardLimitsFromBlurb"/> is true, the low boundary line
    /// ("Flexible boundaries…") is omitted so IdentityBlurb LIMITS are not undercut
    /// (PROP-15.17). That omission does not leave an empty slot.
    /// </summary>
    public static string Compile(PersonaPack pack, bool hardLimitsFromBlurb = false)
    {
        ArgumentNullException.ThrowIfNull(pack);
        var traits = pack.Traits ?? new PersonaTraitScales();

        string? boundaryLine = null;
        var rest = new List<DirectiveCandidate>(PersonaTraitCatalog.All.Count);
        foreach (var trait in PersonaTraitCatalog.All)
        {
            var value = trait.Get(traits);
            if (trait.IsDefault(value))
                continue;

            var band = ToBand(value);
            if (hardLimitsFromBlurb && trait.OmitForHardLimits(band))
                continue;

            var line = trait.Line(band);
            if (ReferenceEquals(trait, PersonaTraitCatalog.Boundary)
                || trait.JsonName == PersonaTraitCatalog.Boundary.JsonName)
            {
                boundaryLine = line;
                continue;
            }

            rest.Add(new DirectiveCandidate(trait.Order, Math.Abs(value - trait.Default), line));
        }

        if (boundaryLine is null && rest.Count == 0)
            return string.Empty;

        rest.Sort(static (a, b) =>
        {
            var byDistance = b.Distance.CompareTo(a.Distance);
            return byDistance != 0 ? byDistance : a.Order.CompareTo(b.Order);
        });

        var selected = new List<string>(MaxDirectiveLines);
        if (boundaryLine is not null)
            selected.Add(boundaryLine);
        foreach (var candidate in rest)
        {
            if (selected.Count >= MaxDirectiveLines)
                break;
            selected.Add(candidate.Text);
        }

        if (selected.Count == 0)
            return string.Empty;

        var sb = new StringBuilder(512);
        sb.Append(Marker);
        foreach (var line in selected)
        {
            sb.Append('\n');
            sb.Append(line);
        }

        return sb.ToString();
    }

    public static string CompileVoice(
        PersonaTraitBand warmth,
        PersonaTraitBand directness,
        PersonaTraitBand formality,
        PersonaTraitBand playfulness)
    {
        var warmthText = Body("warmth", warmth);
        var directText = Body("directness", directness);
        var formalText = Body("formality", formality);
        var playText = Body("playfulness", playfulness);
        return $"{TrimPeriod(warmthText)}; {TrimPeriod(directText)}; {TrimPeriod(formalText)}; {TrimPeriod(playText)}.";
    }

    public static string CompileBoundaries(PersonaTraitBand band) =>
        Body("boundaryStrictness", band);

    public static string CompileRecall(PersonaTraitBand band) =>
        Body("recallBias", band);

    private static string Body(string jsonName, PersonaTraitBand band)
    {
        var trait = PersonaTraitCatalog.All.First(t => t.JsonName == jsonName);
        return trait.Body(band);
    }

    private static string TrimPeriod(string text) =>
        text.EndsWith('.') ? text[..^1] : text;

    private readonly record struct DirectiveCandidate(int Order, double Distance, string Text);
}
