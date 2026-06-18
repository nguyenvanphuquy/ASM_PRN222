using DataAccessLayer.Entities;

namespace ServiceLayer.Dtos;

public record ChatAnswer(string Answer, List<ChatSource> Sources);
