using System;

namespace ServiceLayer.DTOs
{
    public class ChapterDto
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SubjectId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int OrderIndex { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
