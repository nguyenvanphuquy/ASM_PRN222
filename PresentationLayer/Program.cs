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
using ServiceLayer.Mapping;
using DataAccessLayer.Repositories.Implementations;
using DataAccessLayer.Repositories.Interfaces;
using ServiceLayer.Services.Implementations;
using ServiceLayer.Services.Interfaces;

namespace PresentationLayer;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Register AutoMapper
        builder.Services.AddAutoMapper(typeof(MappingProfile).Assembly);

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
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
                sqlOptions => sqlOptions.UseCompatibilityLevel(120)));

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
        builder.Services.AddScoped<IBillingRepository, BillingRepository>();

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
        builder.Services.AddScoped<IBillingService, BillingService>();
        builder.Services.AddScoped<IReportService, ReportService>();
        builder.Services.AddScoped<IModelComparisonService, ModelComparisonService>();
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
            // Chỉ giảng viên ĐƯỢC ADMIN GIAO MÔN (có ít nhất một môn trong AssignedSubjects)
            // mới được upload tài liệu. Admin KHÔNG được upload — mỗi môn chỉ có đúng một
            // giảng viên phụ trách, nhưng một giảng viên có thể phụ trách nhiều môn.
            options.AddPolicy("CanUploadDocuments", p => p.RequireAssertion(ctx =>
                !ctx.User.IsInRole("Admin") &&
                !string.IsNullOrEmpty(ctx.User.FindFirst("AssignedSubjects")?.Value)));
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
        app.MapHub<SubjectsHub>("/hubs/subjects");

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
                    IF COL_LENGTH('Subjects', 'IsDeleted') IS NULL
                        ALTER TABLE Subjects ADD IsDeleted bit NOT NULL DEFAULT 0;
                    IF COL_LENGTH('Subjects', 'CreatedByUserId') IS NULL
                        ALTER TABLE Subjects ADD CreatedByUserId nvarchar(36) NULL;
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
                    IF COL_LENGTH('AllowedEmails', 'AddedByUserId') IS NULL
                        ALTER TABLE AllowedEmails ADD AddedByUserId nvarchar(36) NULL;
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
                    END;
                    IF COL_LENGTH('SystemSettings', 'LastModifiedByUserId') IS NULL
                        ALTER TABLE SystemSettings ADD LastModifiedByUserId nvarchar(36) NULL;
                    IF OBJECT_ID('Notifications') IS NULL
                    BEGIN
                        CREATE TABLE Notifications (
                            Id        nvarchar(36)  NOT NULL PRIMARY KEY,
                            UserId    nvarchar(36)  NOT NULL DEFAULT '',
                            Type      nvarchar(20)  NOT NULL DEFAULT 'info',
                            Title     nvarchar(200) NOT NULL DEFAULT '',
                            Message   nvarchar(max) NOT NULL DEFAULT '',
                            IsRead    bit           NOT NULL DEFAULT 0,
                            CreatedAt datetime2     NOT NULL DEFAULT GETUTCDATE()
                        );
                        CREATE INDEX IX_Notifications_UserId ON Notifications (UserId);
                    END;
                    IF OBJECT_ID('LecturerSubjects') IS NULL
                    BEGIN
                        CREATE TABLE LecturerSubjects (
                            Id        nvarchar(36) NOT NULL PRIMARY KEY,
                            UserId    nvarchar(36) NOT NULL DEFAULT '',
                            SubjectId nvarchar(36) NOT NULL DEFAULT '',
                            CreatedAt datetime2    NOT NULL DEFAULT GETUTCDATE()
                        );
                        CREATE INDEX IX_LecturerSubjects_UserId ON LecturerSubjects (UserId);
                        CREATE UNIQUE INDEX UX_LecturerSubjects_SubjectId ON LecturerSubjects (SubjectId);
                    END;
                    IF OBJECT_ID('TokenUsageLogs') IS NULL
                    BEGIN
                        CREATE TABLE TokenUsageLogs (
                            Id               nvarchar(36)  NOT NULL PRIMARY KEY,
                            UserId           nvarchar(36)  NOT NULL DEFAULT '',
                            SessionId        nvarchar(36)  NULL,
                            Model            nvarchar(100) NOT NULL DEFAULT '',
                            PromptTokens     int           NOT NULL DEFAULT 0,
                            CompletionTokens int           NOT NULL DEFAULT 0,
                            TotalTokens      int           NOT NULL DEFAULT 0,
                            CostUsd          decimal(18,8) NOT NULL DEFAULT 0,
                            Kind             nvarchar(20)  NOT NULL DEFAULT 'chat',
                            CreatedAt        datetime2     NOT NULL DEFAULT GETUTCDATE()
                        );
                        CREATE INDEX IX_TokenUsageLogs_UserId ON TokenUsageLogs (UserId);
                        CREATE INDEX IX_TokenUsageLogs_CreatedAt ON TokenUsageLogs (CreatedAt);
                    END;
                    IF OBJECT_ID('Packages') IS NULL
                    BEGIN
                        CREATE TABLE Packages (
                            Id           nvarchar(36)  NOT NULL PRIMARY KEY,
                            Name         nvarchar(150) NOT NULL DEFAULT '',
                            Description  nvarchar(500) NOT NULL DEFAULT '',
                            PriceVnd     bigint        NOT NULL DEFAULT 0,
                            TokenQuota   int           NOT NULL DEFAULT 0,
                            DurationDays int           NOT NULL DEFAULT 0,
                            IsActive     bit           NOT NULL DEFAULT 1,
                            IsPopular    bit           NOT NULL DEFAULT 0,
                            CreatedAt    datetime2     NOT NULL DEFAULT GETUTCDATE()
                        );
                    END;
                    IF OBJECT_ID('PackagePurchases') IS NULL
                    BEGIN
                        CREATE TABLE PackagePurchases (
                            Id            nvarchar(36)  NOT NULL PRIMARY KEY,
                            UserId        nvarchar(36)  NOT NULL DEFAULT '',
                            PackageId     nvarchar(36)  NOT NULL DEFAULT '',
                            PackageName   nvarchar(150) NOT NULL DEFAULT '',
                            AmountVnd     bigint        NOT NULL DEFAULT 0,
                            TokensGranted int           NOT NULL DEFAULT 0,
                            TokensUsed    int           NOT NULL DEFAULT 0,
                            Status        nvarchar(20)  NOT NULL DEFAULT 'Paid',
                            PaymentMethod nvarchar(30)  NOT NULL DEFAULT 'Mock',
                            TransactionRef nvarchar(60) NOT NULL DEFAULT '',
                            CreatedAt     datetime2     NOT NULL DEFAULT GETUTCDATE(),
                            ExpiresAt     datetime2     NULL
                        );
                        CREATE INDEX IX_PackagePurchases_UserId ON PackagePurchases (UserId);
                        CREATE INDEX IX_PackagePurchases_CreatedAt ON PackagePurchases (CreatedAt);
                    END;");

                var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
                var subjects = scope.ServiceProvider.GetRequiredService<ISubjectService>();
                var billing = scope.ServiceProvider.GetRequiredService<IBillingService>();
                await auth.EnsureSeedUsersAsync();
                await subjects.EnsureSeedAsync();
                await billing.EnsureSeedPackagesAsync();

                // Demo: gán môn PRN222 cho giảng viên nếu chưa có môn nào → hiện nút Upload.
                var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
                var subjectRepo = scope.ServiceProvider.GetRequiredService<ISubjectRepository>();
                var demoLecturer = await userRepo.GetByUsernameAsync("lecturer");
                if (demoLecturer != null)
                {
                    var assigned = await userRepo.GetAssignedSubjectIdsAsync(demoLecturer.Id);
                    if (assigned.Count == 0)
                    {
                        var allSubjects = await subjectRepo.GetAllAsync();
                        var prn = allSubjects.FirstOrDefault(s => s.Code == "PRN222") ?? allSubjects.FirstOrDefault();
                        if (prn != null)
                        {
                            await userRepo.ReplaceAssignedSubjectsAsync(demoLecturer.Id, new[] { prn.Id });
                            demoLecturer.CanUploadDocuments = true;
                            demoLecturer.AssignedSubjectId = prn.Id;
                            await userRepo.UpdateAsync(demoLecturer);
                        }
                    }
                }
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


