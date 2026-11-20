namespace SoulCore.Memory;

/// <summary>
/// One episodic recall hit with its UTC timestamp (PROP-20.2 dated recall).
/// </summary>
public readonly record struct MemoryRecallLine(string Content, string OccurredAt);
