using System;
using System.Collections.Generic;

namespace ServiceLayer.DTOs
{
    public class ChatMessageDto
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SessionId { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public List<ChatSourceDto> Sources { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    public class ChatSourceDto
    {
        public string DocumentId { get; set; } = string.Empty;
        public string DocumentName { get; set; } = string.Empty;
        public int ChunkIndex { get; set; }
        public int Page { get; set; }
        public string Snippet { get; set; } = string.Empty;
        public float ConfidenceScore { get; set; }
    }
}
