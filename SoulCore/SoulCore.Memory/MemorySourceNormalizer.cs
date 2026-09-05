namespace SoulCore.Memory;
internal static class MemorySourceNormalizer {
  private static readonly HashSet<string> AllowedSources = new(StringComparer.OrdinalIgnoreCase){"self","chat","imported","observation","correction","system","model"};
  internal static string Normalize(string? s){ if(string.IsNullOrWhiteSpace(s)) return "system"; var t=s.Trim().ToLowerInvariant(); return AllowedSources.Contains(t)?t:"system"; }
}
