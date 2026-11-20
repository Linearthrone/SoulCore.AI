namespace SoulCore.Core.Abstractions;

/// <summary>
/// Seed record for inserting initial <c>charter_anchors</c> rows.
/// Intended for test/staging seeding only — not wired to the live Host DI.
/// <paramref name="Book"/> defaults to constitution; must be one of the four PROP-20.14 books.
/// </summary>
public sealed record CharterAnchorSeed(
    string Kind,
    string Title,
    string Body,
    int Priority = 100,
    bool IsLocked = false,
    string Source = "seed",
    string Book = "constitution");
