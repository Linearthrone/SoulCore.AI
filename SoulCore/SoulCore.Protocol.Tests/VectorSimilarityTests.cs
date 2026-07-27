using SoulCore.Memory;

namespace SoulCore.Protocol.Tests;

public class VectorSimilarityTests
{
    [Fact]
    public void Cosine_IdenticalUnitVectors_IsOne()
    {
        float[] a = [1f, 0f, 0f];
        float[] b = [1f, 0f, 0f];
        Assert.Equal(1.0, VectorSimilarity.Cosine(a, b), precision: 5);
    }

    [Fact]
    public void Cosine_Orthogonal_IsZero()
    {
        float[] a = [1f, 0f];
        float[] b = [0f, 1f];
        Assert.Equal(0.0, VectorSimilarity.Cosine(a, b), precision: 5);
    }

    [Fact]
    public void Cosine_Opposite_IsNegativeOne()
    {
        float[] a = [1f, 0f];
        float[] b = [-1f, 0f];
        Assert.Equal(-1.0, VectorSimilarity.Cosine(a, b), precision: 5);
    }

    [Fact]
    public void Cosine_DimensionMismatch_IsZero()
    {
        float[] a = [1f, 0f];
        float[] b = [1f, 0f, 0f];
        Assert.Equal(0.0, VectorSimilarity.Cosine(a, b));
    }

    [Fact]
    public void RankByCosineTopK_OrdersBySimilarity()
    {
        float[] query = [1f, 0f, 0f];
        var candidates = new List<(string Item, float[] Vector)>
        {
            ("orthogonal", [0f, 1f, 0f]),
            ("near", [0.9f, 0.1f, 0f]),
            ("opposite", [-1f, 0f, 0f]),
            ("exact", [1f, 0f, 0f])
        };

        var top = VectorSimilarity.RankByCosineTopK(query, candidates, limit: 2);

        Assert.Equal(2, top.Count);
        Assert.Equal("exact", top[0]);
        Assert.Equal("near", top[1]);
    }

    [Fact]
    public void LittleEndianBlob_RoundTrips()
    {
        float[] original = [0.5f, -1.25f, 3.14159f, 0f];
        var blob = VectorSimilarity.ToLittleEndianBlob(original);
        Assert.Equal(original.Length * sizeof(float), blob.Length);

        var restored = VectorSimilarity.FromLittleEndianBlob(blob);
        Assert.Equal(original.Length, restored.Length);
        for (var i = 0; i < original.Length; i++)
            Assert.Equal(original[i], restored[i]);
    }
}
