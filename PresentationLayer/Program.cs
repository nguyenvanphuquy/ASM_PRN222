using DataAccessLayer.Context;
using DataAccessLayer.Repositories;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using PresentationLayer.Hubs;
using PresentationLayer.Services;
using ServiceLayer.Services;
using ServiceLayer.Services.Embeddings;
using ServiceLayer.Settings;

namespace PresentationLayer;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // === Config ===
        builder.Services.Configure<GroqSettings>(builder.Configuration.GetSection("Groq"));
        builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));

        // === Upload size limits (2GB) ===
        const long maxUploadBytes = 2048L * 1024 * 1024;
        builder.Services.Configure<FormOptions>(o =>
        {
            o.MultipartBodyLengthLimit = maxUploadBytes;
            o.ValueLengthLimit = int.MaxValue;
            o.MemoryBufferThreshold = int.MaxValue;
        });
        builder.WebHost.ConfigureKestrel(opt =>
        {
            opt.Limits.MaxRequestBodySize = maxUploadBytes;
        });

        // === Database ===
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

        // === DAL ===
        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<ISubjectRepository, SubjectRepository>();
        builder.Services.AddScoped<IChapterRepository, ChapterRepository>();
        builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
        builder.Services.AddScoped<IDocumentChunkRepository, DocumentChunkRepository>();
        builder.Services.AddScoped<IChatRepository, ChatRepository>();
        builder.Services.AddScoped<IFeedbackRepository, FeedbackRepository>();
        builder.Services.AddScoped<IFeedbackReplyRepository, FeedbackReplyRepository>();
        builder.Services.AddScoped<IAllowedEmailRepository, AllowedEmailRepository>();

        // === Services ===
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<IEmailService, EmailService>();
        builder.Services.AddScoped<IUserService, UserService>();
        builder.Services.AddScoped<ISubjectService, SubjectService>();
        builder.Services.AddScoped<IChapterService, ChapterService>();
        builder.Services.AddScoped<IAllowedEmailService, AllowedEmailService>();
        builder.Services.AddScoped<ISystemSettingService, SystemSettingService>();
        
        builder.Services.AddSingleton<ITextExtractor, TextExtractor>();
        
        // Chunking
        builder.Services.AddSingleton<IChunkingStrategy, SemanticKernelStrategy>();
        builder.Services.AddSingleton<IChunkingStrategy, FixedSizeChunkingStrategy>();
        builder.Services.AddSingleton<IChunkingStrategy, SentenceChunkingStrategy>();
        builder.Services.AddSingleton<IChunkingFactory, ChunkingFactory>();
        
        // Embeddings
        builder.Services.AddHttpClient<IEmbeddingProvider, OpenAIEmbeddingProvider>();
        builder.Services.AddHttpClient<IEmbeddingProvider, HuggingFaceEmbeddingProvider>();
        builder.Services.AddScoped<IEmbeddingFactory>(sp => 
        {
            var providers = sp.GetServices<IEmbeddingProvider>();
            return new EmbeddingFactory(providers);
        });

        builder.Services.AddSingleton<IDocumentFileStore>(_ =>
            new LocalDocumentFileStore(
                Path.Combine(builder.Environment.ContentRootPath, "App_Data", "uploads")));
        builder.Services.AddScoped<IDocumentService, DocumentService>();
        builder.Services.AddScoped<IFeedbackService, FeedbackService>();
        builder.Services.AddScoped<IDashboardService, DashboardService>();
        builder.Services.AddScoped<IChatService, ChatService>();
        builder.Services.AddScoped<IQualityCheckService, QualityCheckService>();
        builder.Services.AddScoped<IChunkingService, ChunkingService>();
        builder.Services.AddScoped<IRetrievalService, RetrievalService>();
        builder.Services.AddScoped<INotificationService, NotificationService>();

        // Groq (HTTP client)
        builder.Services.AddHttpClient<IGroqService, GroqService>(c =>
        {
            c.Timeout = TimeSpan.FromSeconds(60);
        });

        // === File Comparison Service (tính năng AI so sánh file) ===
        builder.Services.AddHttpClient<IFileComparisonService, FileComparisonService>(c =>
        {
            c.Timeout = TimeSpan.FromSeconds(120); // Phân tích 2 file cần thêm thời gian
        });

        // === Auth ===
        builder.Services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Auth/Login";
                options.LogoutPath = "/Auth/Logout";
                options.AccessDeniedPath = "/Auth/AccessDenied";
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
            });

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("LecturerOrAdmin", p => p.RequireRole("Lecturer", "Admin"));
            options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
            options.AddPolicy("CanUploadDocuments", p => p.RequireAssertion(ctx =>
                ctx.User.IsInRole("Admin") || ctx.User.IsInRole("Lecturer") || ctx.User.HasClaim("CanUpload", "true")));
        });

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSession(o =>
        {
            o.IdleTimeout = TimeSpan.FromHours(2);
            o.Cookie.HttpOnly = true;
            o.Cookie.IsEssential = true;
        });

        // === Razor Pages ===
        builder.Services.AddRazorPages();

        // === Controllers (for API + Swagger) ===
        builder.Services.AddControllers();

        // === SignalR ===
        builder.Services.AddSignalR();

        // === Swagger ===
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "ChatBot PRN222 API",
                Version = "v1",
                Description = "REST API cho hệ thống RAG Chatbot học thuật — Chat, Document Management, Quality Check."
            });
            // Cookie auth scheme cho Swagger UI
            c.AddSecurityDefinition("cookieAuth", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Cookie,
                Name = ".AspNetCore.Cookies",
                Description = "Đăng nhập web trước, rồi dùng cookie session để gọi API."
            });
        });

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseSession();
        app.UseAuthentication();
        app.UseAuthorization();

        // === Swagger UI ===
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "ChatBot PRN222 API v1");
            c.RoutePrefix = "swagger";
            c.DocumentTitle = "ChatBot PRN222 – API Docs";
        });

        app.MapRazorPages();
        app.MapControllers();
        app.MapHub<ChatHub>("/hubs/chat");
        app.MapHub<NotificationHub>("/hubs/notifications");

        // === DB Init ===
        using (var scope = app.Services.CreateScope())
        {
            try
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.EnsureCreated();

                db.Database.ExecuteSqlRaw(@"
                    IF COL_LENGTH('Users', 'CanUploadDocuments') IS NULL
                        ALTER TABLE Users ADD CanUploadDocuments bit NOT NULL DEFAULT 0;
                    IF COL_LENGTH('Users', 'AssignedSubjectId') IS NULL
                        ALTER TABLE Users ADD AssignedSubjectId nvarchar(36) NULL;
                    IF COL_LENGTH('Users', 'IsEmailVerified') IS NULL
                        ALTER TABLE Users ADD IsEmailVerified bit NOT NULL DEFAULT 1;
                    IF COL_LENGTH('Users', 'EmailVerificationToken') IS NULL
                        ALTER TABLE Users ADD EmailVerificationToken nvarchar(64) NULL;
                    IF OBJECT_ID('Chapters') IS NULL
                    BEGIN
                        CREATE TABLE Chapters (
                            Id          nvarchar(36)  NOT NULL PRIMARY KEY,
                            SubjectId   nvarchar(36)  NOT NULL DEFAULT '',
                            Title       nvarchar(300) NOT NULL DEFAULT '',
                            Description nvarchar(max) NOT NULL DEFAULT '',
                            OrderIndex  int           NOT NULL DEFAULT 0,
                            CreatedAt   datetime2     NOT NULL DEFAULT GETUTCDATE()
                        );
                        CREATE INDEX IX_Chapters_SubjectId ON Chapters (SubjectId);
                    END;
                    IF COL_LENGTH('Documents', 'ChapterId') IS NULL
                        ALTER TABLE Documents ADD ChapterId nvarchar(36) NULL;
                    IF COL_LENGTH('Documents', 'ExtractedText') IS NULL
                        ALTER TABLE Documents ADD ExtractedText nvarchar(max) NULL;
                    IF COL_LENGTH('Documents', 'QualityScore') IS NULL
                        ALTER TABLE Documents ADD QualityScore int NOT NULL DEFAULT 0;
                    IF COL_LENGTH('Documents', 'QualitySummary') IS NULL
                        ALTER TABLE Documents ADD QualitySummary nvarchar(max) NULL;
                    IF COL_LENGTH('Documents', 'QualityWarnings') IS NULL
                        ALTER TABLE Documents ADD QualityWarnings nvarchar(max) NULL;
                    IF OBJECT_ID('AllowedEmails') IS NULL
                    BEGIN
                        CREATE TABLE AllowedEmails (
                            Id        nvarchar(36)  NOT NULL PRIMARY KEY,
                            Email     nvarchar(200) NOT NULL DEFAULT '',
                            Note      nvarchar(300) NOT NULL DEFAULT '',
                            AddedBy   nvarchar(200) NOT NULL DEFAULT '',
                            CreatedAt datetime2     NOT NULL DEFAULT GETUTCDATE()
                        );
                        CREATE UNIQUE INDEX UX_AllowedEmails_Email ON AllowedEmails (Email);
                    END;
                    IF COL_LENGTH('DocumentChunks', 'VectorJson') IS NULL
                        ALTER TABLE DocumentChunks ADD VectorJson nvarchar(max) NULL;
                    IF COL_LENGTH('DocumentChunks', 'EmbeddingModel') IS NULL
                        ALTER TABLE DocumentChunks ADD EmbeddingModel nvarchar(100) NULL;
                    IF OBJECT_ID('SystemSettings') IS NULL
                    BEGIN
                        CREATE TABLE SystemSettings (
                            Id          nvarchar(36)  NOT NULL PRIMARY KEY,
                            [Key]       nvarchar(100) NOT NULL,
                            Value       nvarchar(max) NULL,
                            Description nvarchar(500) NULL,
                            UpdatedAt   datetime2     NOT NULL DEFAULT GETUTCDATE()
                        );
                        CREATE UNIQUE INDEX UX_SystemSettings_Key ON SystemSettings ([Key]);
                    END;");

                var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
                var subjects = scope.ServiceProvider.GetRequiredService<ISubjectService>();
                await auth.EnsureSeedUsersAsync();
                await subjects.EnsureSeedAsync();
            }
            catch (Exception ex)
            {
                var logger = app.Services.GetRequiredService<ILogger<Program>>();
                logger.LogWarning(ex, "DB init failed — check appsettings.json connection string");
            }
        }

        app.Run();
    }
}
