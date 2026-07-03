using ServiceLayer.Dtos;

namespace ServiceLayer.Services.Interfaces;

/// <summary>
/// Báo cáo &amp; thống kê: tiêu thụ token theo người dùng/tuần/tháng và doanh thu/lợi nhuận gói.
/// </summary>
public interface IReportService
{
    /// <param name="range">week | month | all</param>
    Task<TokenReport> GetTokenReportAsync(string range);
    Task<RevenueReport> GetRevenueReportAsync();
}
