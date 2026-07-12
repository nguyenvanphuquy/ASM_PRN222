using System.Security.Claims;

namespace PresentationLayer.Helpers;

/// <summary>Kiểm tra giảng viên có được quản lý tài liệu của môn đã giao hay không.</summary>
public static class SubjectDocumentAuth
{
    public static bool CanManageSubject(ClaimsPrincipal user, string subjectId)
    {
        if (user.IsInRole("Admin")) return false;
        if (!user.IsInRole("Lecturer")) return false;
        if (string.IsNullOrWhiteSpace(subjectId)) return false;

        var assigned = user.FindFirst("AssignedSubjects")?.Value ?? "";
        if (string.IsNullOrWhiteSpace(assigned)) return false;

        return assigned.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(id => string.Equals(id, subjectId, StringComparison.OrdinalIgnoreCase));
    }

    public static bool HasAssignedSubjects(ClaimsPrincipal user)
        => !string.IsNullOrWhiteSpace(user.FindFirst("AssignedSubjects")?.Value);
}
