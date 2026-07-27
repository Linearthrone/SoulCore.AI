namespace SoulCore.Memory;

/// <summary>
/// In-process cosine similarity for episodic embedding recall (sqlite-vec deferred).
/// </summary>
public static class VectorSimilarity
{
    /// <summary>
    /// Cosine similarity in [-1, 1]. Returns 0 when either vector is empty or zero-norm,
    /// or when dimensions differ.
    /// </summary>
    public static double Cosine(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length == 0 || b.Length == 0 || a.Length != b.Length)
            return 0.0;

        double dot = 0;
        double normA = 0;
        double normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            var av = a[i];
            var bv = b[i];
            dot += av * bv;
            normA += av * av;
            normB += bv * bv;
        }

        if (normA <= 0 || normB <= 0)
            return 0.0;

        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    /// <summary>
    /// Rank candidates by cosine similarity to <paramref name="query"/>; return top
    /// <paramref name="limit"/> items (stable: higher score first; ties keep input order).
    /// </summary>
    public static IReadOnlyList<T> RankByCosineTopK<T>(
        ReadOnlySpan<float> query,
        IReadOnlyList<(T Item, float[] Vector)> candidates,
        int limit)
    {
        if (limit <= 0 || candidates.Count == 0 || query.Length == 0)
            return Array.Empty<T>();

        var scored = new List<(T Item, double Score, int Index)>(candidates.Count);
        for (var i = 0; i < candidates.Count; i++)
        {
            var (item, vector) = candidates[i];
            if (vector is null || vector.Length == 0)
                continue;
            var score = Cosine(query, vector);
            scored.Add((item, score, i));
        }

        scored.Sort((x, y) =>
        {
            var cmp = y.Score.CompareTo(x.Score);
            return cmp != 0 ? cmp : x.Index.CompareTo(y.Index);
        });

        var take = Math.Min(limit, scored.Count);
        var result = new List<T>(take);
        for (var i = 0; i < take; i++)
            result.Add(scored[i].Item);
        return result;
    }

    /// <summary>Serialize float32 vector as little-endian blob.</summary>
    public static byte[] ToLittleEndianBlob(ReadOnlySpan<float> vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        for (var i = 0; i < vector.Length; i++)
        {
            var bits = BitConverter.SingleToInt32Bits(vector[i]);
            if (!BitConverter.IsLittleEndian)
                bits = ReverseEndianness(bits);
            var offset = i * sizeof(float);
            bytes[offset] = (byte)bits;
            bytes[offset + 1] = (byte)(bits >> 8);
            bytes[offset + 2] = (byte)(bits >> 16);
            bytes[offset + 3] = (byte)(bits >> 24);
        }

        return bytes;
    }

    /// <summary>Deserialize little-endian float32 blob.</summary>
    public static float[] FromLittleEndianBlob(ReadOnlySpan<byte> blob)
    {
        if (blob.Length == 0 || blob.Length % sizeof(float) != 0)
            return Array.Empty<float>();

        var count = blob.Length / sizeof(float);
        var vector = new float[count];
        for (var i = 0; i < count; i++)
        {
            var offset = i * sizeof(float);
            var bits = blob[offset]
                       | (blob[offset + 1] << 8)
                       | (blob[offset + 2] << 16)
                       | (blob[offset + 3] << 24);
            if (!BitConverter.IsLittleEndian)
                bits = ReverseEndianness(bits);
            vector[i] = BitConverter.Int32BitsToSingle(bits);
        }

        return vector;
    }

    private static int ReverseEndianness(int value) =>
        (int)((uint)value >> 24)
        | (int)(((uint)value & 0x00FF0000) >> 8)
        | (int)(((uint)value & 0x0000FF00) << 8)
        | (int)((uint)value << 24);
}
