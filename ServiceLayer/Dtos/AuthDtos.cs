namespace ServiceLayer.Dtos;

public record LoginResult(bool Success, string? ErrorMessage, string? UserId, string? Username, string? FullName, string? Role, string? AvatarPath, bool CanUploadDocuments = false, string? AssignedSubjectId = null);
public record RegisterResult(bool Success, string? ErrorMessage);
