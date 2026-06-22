using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DataAccessLayer.Context;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
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
            e.Property(u => u.Role).HasMaxLength(50).HasDefaultValue("Student");
            e.Property(u => u.AvatarPath).HasMaxLength(500);
            e.Property(u => u.AssignedSubjectId).HasMaxLength(36);
            e.Property(u => u.EmailVerificationToken).HasMaxLength(64);

            e.HasOne(u => u.AssignedSubject)
                .WithMany()
                .HasForeignKey(u => u.AssignedSubjectId)
                .OnDelete(DeleteBehavior.Restrict);
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
                .HasColumnType("nvarchar(max)");

            e.HasOne(m => m.Session)
                .WithMany(s => s.Messages)
                .HasForeignKey(m => m.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
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
    }
}
