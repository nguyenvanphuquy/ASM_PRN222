using DataAccessLayer.Repositories;
using ServiceLayer.Dtos;

namespace ServiceLayer.Services;

public class DashboardService : IDashboardService
{
    private readonly IUserService _users;
    private readonly ISubjectRepository _subjects;
    private readonly IDocumentRepository _documents;
    private readonly IDocumentChunkRepository _chunks;
    private readonly IChatRepository _chat;
    private readonly IFeedbackRepository _feedback;
    private readonly IFeedbackReplyRepository _replies;

    public DashboardService(
        IUserService users,
        ISubjectRepository subjects,
        IDocumentRepository documents,
        IDocumentChunkRepository chunks,
        IChatRepository chat,
        IFeedbackRepository feedback,
        IFeedbackReplyRepository replies)
    {
        _users = users;
        _subjects = subjects;
        _documents = documents;
        _chunks = chunks;
        _chat = chat;
        _feedback = feedback;
        _replies = replies;
    }

    public async Task<DashboardStats> GetStatsAsync()
    {
        var (total, admins, lecturers, students) = await _users.GetCountsAsync();
        var docs = await _documents.GetAllAsync();
        var subjects = await _subjects.GetAllAsync();
        var feedbacks = await _feedback.GetAllAsync();

        var repliedFeedbackIds = (await _replies.GetByFeedbackIdsAsync(feedbacks.Select(f => f.Id)))
            .Select(r => r.FeedbackId)
            .Distinct()
            .ToHashSet();

        // Rating distribution (1..5 stars)
        var ratingCounts = new int[5];
        foreach (var f in feedbacks)
            if (f.Rating >= 1 && f.Rating <= 5) ratingCounts[f.Rating - 1]++;

        // Documents grouped by subject (top 8)
        var docsPerSubject = docs
            .GroupBy(d => d.SubjectId)
            .Select(g => new SubjectDocCount
            {
                Label = subjects.FirstOrDefault(s => s.Id == g.Key)?.Code ?? "Khác",
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .Take(8)
            .ToList();

        return new DashboardStats
        {
            UsersTotal = total,
            Admins = admins,
            Lecturers = lecturers,
            Students = students,

            Subjects = await _subjects.CountAsync(),
            Documents = docs.Count,
            DocumentBytes = docs.Sum(d => d.FileSize),
            Chunks = await _chunks.CountAsync(),

            ChatSessions = await _chat.CountSessionsAsync(),
            ChatMessages = await _chat.CountMessagesAsync(),

            FeedbackTotal = feedbacks.Count,
            FeedbackAverage = feedbacks.Count > 0 ? feedbacks.Average(f => (double)f.Rating) : 0,
            FeedbackReplies = await _replies.CountAsync(),
            FeedbackAwaiting = feedbacks.Count(f => !repliedFeedbackIds.Contains(f.Id)),

            RecentDocuments = docs.Take(5).ToList(),
            RecentFeedback = feedbacks.Take(5).ToList(),

            RatingCounts = ratingCounts,
            DocumentsPerSubject = docsPerSubject
        };
    }
}
