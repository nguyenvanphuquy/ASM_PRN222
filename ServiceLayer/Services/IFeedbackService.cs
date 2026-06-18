using DataAccessLayer.Entities;

namespace ServiceLayer.Services;

public interface IFeedbackService
{
    Task<List<Feedback>> GetAllAsync();
    Task<List<Feedback>> GetByUserAsync(string userId);
    Task<Feedback> CreateAsync(string userId, string userName, string? userAvatar, int rating, string content);

    // Threaded replies — anyone can reply, admin replies are flagged.
    Task<List<FeedbackReply>> GetRepliesForAsync(IEnumerable<string> feedbackIds);
    Task AddReplyAsync(string feedbackId, string userId, string userName, string? userAvatar, string content, bool isAdmin);
    Task<FeedbackReply?> GetReplyAsync(string replyId);
    Task DeleteReplyAsync(string replyId);

    Task DeleteAsync(string feedbackId);
    Task<(int Total, double Average)> GetStatsAsync();
}
