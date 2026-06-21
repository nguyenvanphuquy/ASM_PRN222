using AutoMapper;
using DataAccessLayer.Entities;
using ServiceLayer.DTOs;

namespace ServiceLayer.Mapping
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<User, UserDto>().ReverseMap();
            CreateMap<Document, DocumentDto>().ReverseMap();
            CreateMap<DocumentChunk, DocumentChunkDto>().ReverseMap();
            CreateMap<Subject, SubjectDto>().ReverseMap();
            CreateMap<Chapter, ChapterDto>().ReverseMap();
            CreateMap<ChatSession, ChatSessionDto>().ReverseMap();
            CreateMap<ChatMessage, ChatMessageDto>().ReverseMap();
            CreateMap<ChatSource, ChatSourceDto>().ReverseMap();
            CreateMap<Notification, NotificationDto>().ReverseMap();
        }
    }
}


