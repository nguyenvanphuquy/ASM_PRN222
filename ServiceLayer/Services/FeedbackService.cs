using DataAccessLayer.Entities;
using DataAccessLayer.Repositories;

namespace ServiceLayer.Services;

public class FeedbackService : IFeedbackService
{
    private readonly IFeedbackRepository _repo;
    private readonly IFeedbackReplyRepository _replyRepo;

    public FeedbackService(IFeedbackRepository repo, IFeedbackReplyRepository replyRepo)
    {
        _repo = repo;
        _replyRepo = replyRepo;
    }

    public Task<List<Feedback>> GetAllAsync() => _repo.GetAllAsync();
    public Task<List<Feedback>> GetByUserAsync(string userId) => _repo.GetByUserAsync(userId);

    public async Task<Feedback> CreateAsync(string userId, string userName, string? userAvatar, int rating, string content)
    {
        if (rating < 1) rating = 1;
        if (rating > 5) rating = 5;

        var feedback = new Feedback
        {
            UserId = userId,
            UserName = userName,
            UserAvatar = userAvatar,
            Rating = rating,
            Content = content?.Trim() ?? string.Empty
        };
        await _repo.CreateAsync(feedback);
        return feedback;
    }

    public Task<List<FeedbackReply>> GetRepliesForAsync(IEnumerable<string> feedbackIds)
        => _replyRepo.GetByFeedbackIdsAsync(feedbackIds);

    public async Task AddReplyAsync(string feedbackId, string userId, string userName, string? userAvatar, string content, bool isAdmin)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        // Ignore replies to a feedback that no longer exists.
        var feedback = await _repo.GetByIdAsync(feedbackId);
        if (feedback == null) return;

        await _replyRepo.CreateAsync(new FeedbackReply
        {
            FeedbackId = feedbackId,
            UserId = userId,
            UserName = userName,
            UserAvatar = userAvatar,
            Content = content.Trim(),
            IsAdmin = isAdmin
        });
    }

    public Task<FeedbackReply?> GetReplyAsync(string replyId) => _replyRepo.GetByIdAsync(replyId);
    public Task DeleteReplyAsync(string replyId) => _replyRepo.DeleteAsync(replyId);

    public async Task DeleteAsync(string feedbackId)
    {
        await _replyRepo.DeleteByFeedbackAsync(feedbackId);
        await _repo.DeleteAsync(feedbackId);
    }

    public async Task<(int Total, double Average)> GetStatsAsync()
        => (await _repo.CountAsync(), await _repo.AverageRatingAsync());
}
