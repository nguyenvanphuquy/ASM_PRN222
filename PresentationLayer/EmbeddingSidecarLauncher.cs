using System.Diagnostics;

namespace PresentationLayer;

/// <summary>
/// Tự khởi động sidecar embedding (tools/embedding_server.py) khi app chạy, và tắt nó khi
/// app dừng — để người dùng không phải nhớ chạy 2 tiến trình. Nếu sidecar đã chạy sẵn
/// (health check OK) hoặc không tìm thấy Python/script thì bỏ qua (app vẫn chạy bình thường;
/// benchmark embedding sẽ báo lỗi rõ nếu sidecar không lên).
/// Tắt bằng cấu hình: "Embedding:AutoStartSidecar": false.
/// </summary>
public class EmbeddingSidecarLauncher : IHostedService
{
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;
    private readonly ILogger<EmbeddingSidecarLauncher> _logger;
    private Process? _proc;

    public EmbeddingSidecarLauncher(IWebHostEnvironment env, IConfiguration config, ILogger<EmbeddingSidecarLauncher> logger)
    {
        _env = env;
        _config = config;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_config.GetValue("Embedding:AutoStartSidecar", true))
            return;

        var port = _config.GetValue("Embedding:SidecarPort", 8600);
        var baseUrl = $"http://127.0.0.1:{port}";

        if (await IsUpAsync(baseUrl))
        {
            _logger.LogInformation("Embedding sidecar đã chạy sẵn tại {Url} — không cần khởi động lại.", baseUrl);
            return;
        }

        var script = FindScript(_env.ContentRootPath);
        if (script == null)
        {
            _logger.LogWarning("Không tìm thấy tools/embedding_server.py — bỏ qua auto-start sidecar.");
            return;
        }

        var workDir = Path.GetDirectoryName(Path.GetDirectoryName(script))!; // gốc solution
        foreach (var python in PythonCandidates())
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = python,
                    WorkingDirectory = workDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                psi.ArgumentList.Add(script);
                psi.Environment["PYTHONUTF8"] = "1";
                psi.Environment["PYTHONIOENCODING"] = "utf-8";
                psi.Environment["EMBED_PORT"] = port.ToString();

                var p = Process.Start(psi);
                if (p == null) continue;

                p.OutputDataReceived += (_, e) => { if (e.Data != null) _logger.LogInformation("[embed-sidecar] {Line}", e.Data); };
                p.ErrorDataReceived += (_, e) => { if (e.Data != null) _logger.LogInformation("[embed-sidecar] {Line}", e.Data); };
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();

                _proc = p;
                _logger.LogInformation("Đã khởi động embedding sidecar (python='{Py}', pid={Pid}) tại {Url}. " +
                    "Lần đầu sẽ tự tải model (~vài GB).", python, p.Id, baseUrl);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Thử python '{Py}' thất bại, chuyển ứng viên khác.", python);
            }
        }

        _logger.LogWarning("Không khởi động được embedding sidecar (không thấy Python?). " +
            "Chạy tay: python tools/embedding_server.py — hoặc benchmark embedding sẽ báo lỗi.");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_proc is { HasExited: false })
            {
                _proc.Kill(entireProcessTree: true);
                _logger.LogInformation("Đã dừng embedding sidecar.");
            }
        }
        catch { /* best-effort */ }
        return Task.CompletedTask;
    }

    private static async Task<bool> IsUpAsync(string baseUrl)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var res = await http.GetAsync($"{baseUrl}/health");
            return res.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    private static string? FindScript(string start)
    {
        var dir = new DirectoryInfo(start);
        for (int i = 0; i < 5 && dir != null; i++, dir = dir.Parent)
        {
            var p = Path.Combine(dir.FullName, "tools", "embedding_server.py");
            if (File.Exists(p)) return p;
        }
        return null;
    }

    private static IEnumerable<string> PythonCandidates()
    {
        yield return "python";
        yield return "py";
        foreach (var p in new[] { @"C:\Python313\python.exe", @"C:\Python312\python.exe" })
            if (File.Exists(p)) yield return p;
    }
}
