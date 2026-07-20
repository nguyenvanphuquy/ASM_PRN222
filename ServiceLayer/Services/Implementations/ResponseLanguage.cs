using System.Globalization;

namespace ServiceLayer.Services.Implementations;

/// <summary>
/// Resolves the language selected in the chat UI before the LLM is called.
/// Keeping this deterministic prevents the language of an uploaded document or
/// a previous chat turn from overriding the user's current choice.
/// </summary>
public static class ResponseLanguage
{
    public const string RefusalMessage = "Tôi không tìm thấy thông tin này trong tài liệu môn học.";

    public static string Resolve(string? preference, string question)
    {
        return preference?.Trim().ToLowerInvariant() switch
        {
            "en" => "en",
            "auto" => DetectQuestionLanguage(question),
            _ => "vi",
        };
    }

    public static bool IsRefusal(string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer)) return false;
        return answer.Contains(RefusalMessage, StringComparison.OrdinalIgnoreCase)
            // Recognise responses stored by the previous English-only version.
            || answer.Contains("I cannot find this information in the course documents.", StringComparison.OrdinalIgnoreCase);
    }

    public static string PromptDirective(string language) => language == "en"
        ? "OUTPUT LANGUAGE — ENGLISH ONLY: write the complete normal answer in English, even when the question, history, or document context is Vietnamese. The exact refusal sentence remains Vietnamese."
        : "NGÔN NGỮ ĐẦU RA — CHỈ TIẾNG VIỆT: viết toàn bộ câu trả lời thông thường bằng tiếng Việt, kể cả khi câu hỏi, lịch sử, hoặc tài liệu bằng tiếng Anh. Câu từ chối giữ nguyên tiếng Việt.";

    private static string DetectQuestionLanguage(string question)
    {
        if (string.IsNullOrWhiteSpace(question)) return "vi";

        // Vietnamese letters/diacritics are an unambiguous signal for the
        // supported Vietnamese/English modes.
        if (question.Any(ch => ch is 'đ' or 'Đ' ||
            CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark || ch > 127))
            return "vi";

        var words = question.ToLowerInvariant()
            .Split(new[] { ' ', '\t', '\r', '\n', '.', ',', '?', '!', ';', ':', '(', ')', '[', ']', '"', '\'', '-', '/' },
                StringSplitOptions.RemoveEmptyEntries);
        var vietnamese = new HashSet<string>(StringComparer.Ordinal)
        {
            "toi", "ban", "minh", "cho", "cua", "la", "gi", "tai", "lieu", "nhu", "the", "khong",
            "duoc", "hay", "ve", "trong", "chuong", "bai", "tong", "quan", "tom", "tat", "dinh", "nghia",
        };
        var english = new HashSet<string>(StringComparer.Ordinal)
        {
            "what", "is", "are", "explain", "describe", "please", "how", "why", "when", "where", "the",
            "a", "an", "of", "for", "with", "about", "give", "list", "tell", "can", "could", "does", "do",
        };

        return words.Count(english.Contains) > words.Count(vietnamese.Contains) ? "en" : "vi";
    }
}
