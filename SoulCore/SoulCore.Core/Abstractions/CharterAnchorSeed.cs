namespace SoulCore.Core.Abstractions;

/// <summary>
/// Seed record for inserting initial <c>charter_anchors</c> rows.
/// Intended for test/staging seeding only — not wired to the live Host DI.
/// </summary>
public sealed record CharterAnchorSeed(
    string Kind,
    string Title,
    string Body,
    int Priority = 100,
    bool IsLocked = false,
    string Source = "seed");
