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
    public DbSet<AllowedEmail> AllowedEmails => Set<AllowedEmail>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();
    public DbSet<FeedbackReply> FeedbackReplies => Set<FeedbackReply>();

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
        });

        modelBuilder.Entity<Subject>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Id).HasMaxLength(36);
            e.Property(s => s.Code).HasMaxLength(50);
            e.Property(s => s.Name).HasMaxLength(200);
        });

        modelBuilder.Entity<Chapter>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).HasMaxLength(36);
            e.Property(c => c.SubjectId).HasMaxLength(36);
            e.Property(c => c.Title).HasMaxLength(300).HasDefaultValue("");
            e.Property(c => c.Description).HasColumnType("nvarchar(max)");
            e.HasIndex(c => c.SubjectId);
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
            e.HasIndex(d => d.SubjectId);
            e.HasIndex(d => d.ChapterId);
        });

        modelBuilder.Entity<DocumentChunk>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).HasMaxLength(36);
            e.Property(c => c.DocumentId).HasMaxLength(36);
            e.Property(c => c.SubjectId).HasMaxLength(36);
            e.Property(c => c.DocumentName).HasMaxLength(500);
            e.Property(c => c.Content).HasColumnType("nvarchar(max)");
            e.HasIndex(c => c.SubjectId);
            e.HasIndex(c => c.DocumentId);
        });

        modelBuilder.Entity<ChatSession>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Id).HasMaxLength(36);
            e.Property(s => s.UserId).HasMaxLength(36);
            e.Property(s => s.SubjectId).HasMaxLength(36);
            e.Property(s => s.Title).HasMaxLength(500);
            e.HasIndex(s => s.UserId);
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
        });

        modelBuilder.Entity<AllowedEmail>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).HasMaxLength(36);
            e.Property(a => a.Email).HasMaxLength(200);
            e.Property(a => a.Note).HasMaxLength(300).HasDefaultValue("");
            e.Property(a => a.AddedBy).HasMaxLength(200).HasDefaultValue("");
            e.HasIndex(a => a.Email).IsUnique();
        });
    }
}
