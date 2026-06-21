using DataAccessLayer.Entities;

namespace DataAccessLayer.Repositories;

public interface IFeedbackReplyRepository
{
    Task CreateAsync(FeedbackReply reply);
    Task<List<FeedbackReply>> GetByFeedbackIdsAsync(IEnumerable<string> feedbackIds);
    Task<FeedbackReply?> GetByIdAsync(string id);
    Task DeleteAsync(string id);
    Task DeleteByFeedbackAsync(string feedbackId);
    Task<int> CountAsync();
}



