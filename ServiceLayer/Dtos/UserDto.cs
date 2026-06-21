using System;

namespace ServiceLayer.DTOs
{
    public class UserDto
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = "Student";
        public bool CanUploadDocuments { get; set; }
        public string? AssignedSubjectId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? AvatarPath { get; set; }
        public string? Bio { get; set; }
        public bool IsEmailVerified { get; set; }
    }
}
