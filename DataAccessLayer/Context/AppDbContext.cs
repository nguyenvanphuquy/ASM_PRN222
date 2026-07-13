using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
namespace DataAccessLayer.Context;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Chapter> Chapters => Set<Chapter>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<AllowedEmail> AllowedEmails => Set<AllowedEmail>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();
    public DbSet<FeedbackReply> FeedbackReplies => Set<FeedbackReply>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<LecturerSubject> LecturerSubjects => Set<LecturerSubject>();
    public DbSet<TokenUsageLog> TokenUsageLogs => Set<TokenUsageLog>();
    public DbSet<Package> Packages => Set<Package>();
    public DbSet<PackagePurchase> PackagePurchases => Set<PackagePurchase>();
    public DbSet<ExperimentRun> ExperimentRuns => Set<ExperimentRun>();
    public DbSet<ExperimentVariant> ExperimentVariants => Set<ExperimentVariant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Username).IsUnique();
            e.Property(u => u.Id).HasMaxLength(36);
            e.Property(u => u.Username).HasMaxLength(100);
            e.Property(u => u.Email).HasMaxLength(200);
            e.Property(u => u.FullName).HasMaxLength(200);
            e.Property(u => u.RoleId).HasMaxLength(36);
            e.Property(u => u.AvatarPath).HasMaxLength(500);
            e.Property(u => u.AssignedSubjectId).HasMaxLength(36);
            e.Property(u => u.EmailVerificationToken).HasMaxLength(64);
            e.Ignore(u => u.Role);

            e.HasOne(u => u.RoleNavigation)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(u => u.AssignedSubject)
                .WithMany()
                .HasForeignKey(u => u.AssignedSubjectId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Role>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Id).HasMaxLength(36);
            e.Property(r => r.Name).HasMaxLength(50);
            e.Property(r => r.Description).HasMaxLength(200);
            e.HasIndex(r => r.Name).IsUnique();
        });

        modelBuilder.Entity<Subject>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Id).HasMaxLength(36);
            e.Property(s => s.Code).HasMaxLength(50);
            e.Property(s => s.Name).HasMaxLength(200);
            e.Property(s => s.CreatedByUserId).HasMaxLength(36);

            e.HasOne<User>()
             .WithMany()
             .HasForeignKey(s => s.CreatedByUserId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasQueryFilter(s => !s.IsDeleted);
        });

        modelBuilder.Entity<Chapter>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).HasMaxLength(36);
            e.Property(c => c.SubjectId).HasMaxLength(36);
            e.Property(c => c.Title).HasMaxLength(300).HasDefaultValue("");
            e.Property(c => c.Description).HasColumnType("nvarchar(max)");
            e.HasIndex(c => c.SubjectId);

            e.HasOne(c => c.Subject)
                .WithMany(s => s.Chapters)
                .HasForeignKey(c => c.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasQueryFilter(c => c.Subject != null && !c.Subject.IsDeleted);
        });

        modelBuilder.Entity<Document>(e =>
        {
            e.HasKey(d => d.Id);
            e.Property(d => d.Id).HasMaxLength(36);
            e.Property(d => d.Title).HasMaxLength(500).HasDefaultValue("");
            e.Property(d => d.SubjectId).HasMaxLength(36);
            e.Property(d => d.ChapterId).HasMaxLength(36);
            e.Property(d => d.UploadedBy).HasMaxLength(36);
            e.Property(d => d.FileName).HasMaxLength(500);
            e.Property(d => d.ContentType).HasMaxLength(100);
            e.Property(d => d.ContentHash).HasMaxLength(64).HasDefaultValue("");
            e.Property(d => d.Status).HasMaxLength(20).HasDefaultValue("Indexed");
            e.Property(d => d.ExtractedText).HasColumnType("nvarchar(max)");
            e.Property(d => d.QualitySummary).HasColumnType("nvarchar(max)");
            e.Property(d => d.QualityWarnings).HasColumnType("nvarchar(max)");
            e.HasIndex(d => d.SubjectId);
            e.HasIndex(d => d.ChapterId);

            e.HasOne(d => d.Subject)
                .WithMany(s => s.Documents)
                .HasForeignKey(d => d.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(d => d.Chapter)
                .WithMany()
                .HasForeignKey(d => d.ChapterId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(d => d.Uploader)
                .WithMany()
                .HasForeignKey(d => d.UploadedBy)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasQueryFilter(d => d.Subject != null && !d.Subject.IsDeleted);
        });

        modelBuilder.Entity<DocumentChunk>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).HasMaxLength(36);
            e.Property(c => c.DocumentId).HasMaxLength(36);
            e.Property(c => c.SubjectId).HasMaxLength(36);
            e.Property(c => c.DocumentName).HasMaxLength(500);
            e.Property(c => c.Content).HasColumnType("nvarchar(max)");
            e.Property(c => c.VectorJson).HasColumnType("nvarchar(max)");
            e.Property(c => c.EmbeddingModel).HasMaxLength(100);
            e.HasIndex(c => c.SubjectId);
            e.HasIndex(c => c.DocumentId);

            e.HasOne(c => c.Document)
                .WithMany()
                .HasForeignKey(c => c.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(c => c.Subject)
                .WithMany()
                .HasForeignKey(c => c.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasQueryFilter(c => c.Subject != null && !c.Subject.IsDeleted);
        });

        modelBuilder.Entity<SystemSetting>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Id).HasMaxLength(36);
            e.Property(s => s.Key).HasMaxLength(100);
            e.HasIndex(s => s.Key).IsUnique();
            e.Property(s => s.Value).HasColumnType("nvarchar(max)");
            e.Property(s => s.Description).HasMaxLength(500);
            e.Property(s => s.LastModifiedByUserId).HasMaxLength(36);

            e.HasOne<User>()
             .WithMany()
             .HasForeignKey(s => s.LastModifiedByUserId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ChatSession>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Id).HasMaxLength(36);
            e.Property(s => s.UserId).HasMaxLength(36);
            e.Property(s => s.SubjectId).HasMaxLength(36);
            e.Property(s => s.Title).HasMaxLength(500);
            e.HasIndex(s => s.UserId);

            e.HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(s => s.Subject)
                .WithMany()
                .HasForeignKey(s => s.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasQueryFilter(s => s.SubjectId == null || (s.Subject != null && !s.Subject.IsDeleted));
        });

        modelBuilder.Entity<ChatMessage>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.Id).HasMaxLength(36);
            e.Property(m => m.SessionId).HasMaxLength(36);
            e.Property(m => m.Role).HasMaxLength(20);
            e.Property(m => m.Content).HasColumnType("nvarchar(max)");
            e.HasIndex(m => m.SessionId);
            e.Property(m => m.Sources)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<ChatSource>>(v, (JsonSerializerOptions?)null) ?? new List<ChatSource>()
                )
                .HasColumnType("nvarchar(max)")
                .Metadata.SetValueComparer(new ValueComparer<List<ChatSource>>(
                    (c1, c2) => JsonSerializer.Serialize(c1, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(c2, (JsonSerializerOptions?)null),
                    c => c == null ? 0 : JsonSerializer.Serialize(c, (JsonSerializerOptions?)null).GetHashCode(),
                    c => JsonSerializer.Deserialize<List<ChatSource>>(JsonSerializer.Serialize(c, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!
                ));

            e.HasOne(m => m.Session)
                .WithMany(s => s.Messages)
                .HasForeignKey(m => m.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasQueryFilter(m => m.Session != null && (m.Session.SubjectId == null || (m.Session.Subject != null && !m.Session.Subject.IsDeleted)));
        });

        modelBuilder.Entity<Feedback>(e =>
        {
            e.HasKey(f => f.Id);
            e.Property(f => f.Id).HasMaxLength(36);
            e.Property(f => f.UserId).HasMaxLength(36);
            e.Property(f => f.UserName).HasMaxLength(200);
            e.Property(f => f.UserAvatar).HasMaxLength(500);
            e.Property(f => f.Content).HasColumnType("nvarchar(max)");
            e.Property(f => f.AdminReply).HasColumnType("nvarchar(max)");
            e.Property(f => f.RepliedBy).HasMaxLength(200);
            e.Property(f => f.RepliedByAvatar).HasMaxLength(500);
            e.HasIndex(f => f.UserId);

            e.HasOne(f => f.User)
                .WithMany()
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FeedbackReply>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Id).HasMaxLength(36);
            e.Property(r => r.FeedbackId).HasMaxLength(36);
            e.Property(r => r.UserId).HasMaxLength(36);
            e.Property(r => r.UserName).HasMaxLength(200);
            e.Property(r => r.UserAvatar).HasMaxLength(500);
            e.Property(r => r.Content).HasColumnType("nvarchar(max)");
            e.HasIndex(r => r.FeedbackId);

            e.HasOne(r => r.Feedback)
                .WithMany(f => f.Replies)
                .HasForeignKey(r => r.FeedbackId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AllowedEmail>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).HasMaxLength(36);
            e.Property(a => a.Email).HasMaxLength(200);
            e.Property(a => a.Note).HasMaxLength(300).HasDefaultValue("");
            e.Property(a => a.AddedBy).HasMaxLength(200).HasDefaultValue("");
            e.Property(a => a.AddedByUserId).HasMaxLength(36);
            e.HasIndex(a => a.Email).IsUnique();

            e.HasOne<User>()
             .WithMany()
             .HasForeignKey(a => a.AddedByUserId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Notification>(e =>
        {
            e.HasKey(n => n.Id);
            e.Property(n => n.Id).HasMaxLength(36);
            e.Property(n => n.UserId).HasMaxLength(36);
            e.Property(n => n.Type).HasMaxLength(20);
            e.Property(n => n.Title).HasMaxLength(200);
            e.Property(n => n.Message).HasColumnType("nvarchar(max)");
            e.HasIndex(n => n.UserId);

            e.HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LecturerSubject>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(36);
            e.Property(x => x.UserId).HasMaxLength(36);
            e.Property(x => x.SubjectId).HasMaxLength(36);
            e.HasIndex(x => x.UserId);
            // Mỗi môn chỉ được giao cho ĐÚNG MỘT giảng viên.
            e.HasIndex(x => x.SubjectId).IsUnique();

            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TokenUsageLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(36);
            e.Property(x => x.UserId).HasMaxLength(36);
            e.Property(x => x.SessionId).HasMaxLength(36);
            e.Property(x => x.Model).HasMaxLength(100);
            e.Property(x => x.Kind).HasMaxLength(20);
            e.Property(x => x.CostUsd).HasColumnType("decimal(18,8)");
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<Package>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(36);
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Description).HasMaxLength(500);
            // Xoá mềm: ẩn gói đã xoá khỏi mọi truy vấn (cửa hàng, quản trị, mua, đếm)
            // nhưng vẫn giữ row để PackagePurchases tham chiếu (khoá ngoại).
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<PackagePurchase>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(36);
            e.Property(x => x.UserId).HasMaxLength(36);
            e.Property(x => x.PackageId).HasMaxLength(36);
            e.Property(x => x.PackageName).HasMaxLength(150);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.PaymentMethod).HasMaxLength(30);
            e.Property(x => x.TransactionRef).HasMaxLength(60);
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.CreatedAt);

            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExperimentRun>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(36);
            e.Property(x => x.Kind).HasMaxLength(20);
            e.Property(x => x.Question).HasMaxLength(1000);
            e.Property(x => x.SubjectId).HasMaxLength(36);
            e.Property(x => x.SubjectName).HasMaxLength(300);
            e.Property(x => x.UserId).HasMaxLength(36);
            e.Property(x => x.WinnerLabel).HasMaxLength(200);
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => x.Kind);
            e.HasMany(x => x.Variants).WithOne(v => v.Run).HasForeignKey(v => v.ExperimentRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExperimentVariant>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(36);
            e.Property(x => x.ExperimentRunId).HasMaxLength(36);
            e.Property(x => x.VariantKey).HasMaxLength(100);
            e.Property(x => x.VariantLabel).HasMaxLength(200);
            e.Property(x => x.AnswerPreview).HasMaxLength(500);
            e.HasIndex(x => x.ExperimentRunId);
        });
    }
}
