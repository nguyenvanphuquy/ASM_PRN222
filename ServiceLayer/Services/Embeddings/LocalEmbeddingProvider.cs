using System.Globalization;
using System.Text;

namespace ServiceLayer.Services.Embeddings;

/// <summary>
/// Embedding chạy hoàn toàn offline, KHÔNG cần API key — dùng kỹ thuật "feature hashing"
/// (giống HashingVectorizer của sklearn): băm các token (đã bỏ dấu) + 3-gram ký tự vào một
/// vector cố định rồi chuẩn hoá L2. Nhờ vậy cosine similarity phản ánh độ trùng lặp từ vựng.
///
/// Mục đích: để module RBL (benchmark embedding / chunking) chạy được ngay mà không phụ thuộc
/// OpenAI/HuggingFace token. Đây là embedding TỪ VỰNG (không phải model ngữ nghĩa neural), nên
/// để so sánh các model thật (e5, bge-m3, PhoBERT) hãy cấu hình token trong Cài đặt.
/// </summary>
public class LocalEmbeddingProvider : IEmbeddingProvider
{
    public const int Dim = 256;
    public string Name => "Local";

    public Task<float[]> GetEmbeddingAsync(string text, string model)
        => Task.FromResult(Embed(text));

    private static float[] Embed(string text)
    {
        var vec = new float[Dim];
        if (string.IsNullOrWhiteSpace(text)) return vec;

        foreach (var token in Tokenize(text))
        {
            AddFeature(vec, "w:" + token, 1.0f);
            var padded = "^" + token + "$";
            for (int i = 0; i + 3 <= padded.Length; i++)
                AddFeature(vec, "g:" + padded.Substring(i, 3), 0.5f);
        }
        return Normalize(vec);
    }

    private static void AddFeature(float[] vec, string feature, float weight)
    {
        uint h = Fnv1a(feature);
        int bucket = (int)(h % (uint)Dim);
        float sign = (h & 0x80000000u) != 0 ? -1f : 1f;
        vec[bucket] += sign * weight;
    }

    private static uint Fnv1a(string s)
    {
        const uint offset = 2166136261, prime = 16777619;
        uint hash = offset;
        foreach (var ch in s) { hash ^= ch; hash *= prime; }
        return hash;
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        foreach (var raw in RemoveDiacritics(text).Split(Sep, StringSplitOptions.RemoveEmptyEntries))
            if (raw.Length >= 2) yield return raw.ToLowerInvariant();
    }

    private static readonly char[] Sep =
        { ' ', '\t', '\n', '\r', '.', ',', '?', '!', ';', ':', '(', ')', '[', ']', '"', '\'', '-', '/', '\\', '*', '#', '“', '”' };

    private static float[] Normalize(float[] v)
    {
        double norm = 0;
        foreach (var x in v) norm += x * (double)x;
        if (norm == 0) return v;
        var inv = (float)(1.0 / Math.Sqrt(norm));
        for (int i = 0; i < v.Length; i++) v[i] *= inv;
        return v;
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        return sb.ToString().Normalize(NormalizationForm.FormC).Replace('đ', 'd').Replace('Đ', 'D');
    }
}
