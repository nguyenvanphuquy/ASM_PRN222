using DataAccessLayer.Repositories.Interfaces;
using ServiceLayer.Services.Interfaces;

namespace PresentationLayer;

/// <summary>
/// Job nền bảo trì hạn dùng gói:
///  1. Đánh dấu Status = "Expired" cho các gói Paid đã quá hạn (không còn để mãi là "Paid").
///  2. Nhắc "sắp hết hạn" (trong 3 ngày) cho người dùng — mỗi giao dịch chỉ nhắc 1 lần.
/// Chạy lần đầu ~20s sau khi app lên, rồi lặp mỗi 6 giờ.
/// </summary>
public class BillingMaintenanceService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BillingMaintenanceService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);
    private const int NearExpiryDays = 3;

    public BillingMaintenanceService(IServiceScopeFactory scopeFactory, ILogger<BillingMaintenanceService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Chờ DB/EnsureCreated sẵn sàng trước khi chạy lần đầu.
        try { await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (Exception ex) { _logger.LogWarning(ex, "BillingMaintenance lỗi khi bảo trì hạn gói"); }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IBillingRepository>();
        var notifier = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var expired = await repo.MarkExpiredAsync();
        if (expired > 0)
            _logger.LogInformation("BillingMaintenance: đánh dấu {Count} gói hết hạn.", expired);

        var near = await repo.GetNearExpiryUnnotifiedAsync(NearExpiryDays);
        foreach (var p in near)
        {
            if (ct.IsCancellationRequested) break;
            var when = p.ExpiresAt!.Value.ToLocalTime().ToString("dd/MM/yyyy");
            await notifier.SendAsync(p.UserId, "warning", "Gói sắp hết hạn",
                $"Gói \"{p.PackageName}\" của bạn sẽ hết hạn ngày {when}. Mua thêm để không gián đoạn hỏi đáp.");
        }
        if (near.Count > 0)
            await repo.MarkExpiryNotifiedAsync(near.Select(x => x.Id));
    }
}
