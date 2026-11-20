namespace SoulCore.Core.Abstractions;

/// <summary>
/// Structured charter anchor row for Settings / Identity surfaces (TASK-177).
/// Read-only projection of <c>charter_anchors</c> — no fabricated biography.
/// <paramref name="Book"/> is constitution / personality / attributes / compass when
/// PROP-20.14 schema is present; null means resolve via <c>CharterBooks</c> by title.
/// </summary>
public sealed record CharterAnchorInfo(
    long Id,
    string Kind,
    string Title,
    string Body,
    int Priority,
    bool IsLocked,
    string Source,
    string? Book = null);
