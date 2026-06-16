using System.Collections.Generic;
using System.Reflection.Emit;
using Microsoft.EntityFrameworkCore;
using PMG201c.Backend.Models;

namespace PMG201c.Backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<AssessmentFile> AssessmentFiles => Set<AssessmentFile>();
    public DbSet<RubricItem> RubricItems => Set<RubricItem>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<GradingJob> GradingJobs => Set<GradingJob>();
    public DbSet<GradingResult> GradingResults => Set<GradingResult>();
    public DbSet<GradingResultItem> GradingResultItems => Set<GradingResultItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();

            entity.Property(e => e.FullName).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(150).IsRequired();
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.Role).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(30).IsRequired();
        });

        modelBuilder.Entity<Assessment>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Title).HasMaxLength(255).IsRequired();
            entity.Property(e => e.CourseCode).HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();

            entity.HasOne(e => e.Teacher)
                .WithMany(e => e.Assessments)
                .HasForeignKey(e => e.TeacherId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AssessmentFile>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.FileType).HasMaxLength(30).IsRequired();
            entity.Property(e => e.OriginalFileName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.StoragePath).HasMaxLength(500).IsRequired();
            entity.Property(e => e.MimeType).HasMaxLength(100);
            entity.Property(e => e.ExtractionStatus).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ExtractionError).HasMaxLength(1000);

            entity.HasOne(e => e.Assessment)
                .WithMany(e => e.Files)
                .HasForeignKey(e => e.AssessmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RubricItem>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Title).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(2000);

            entity.HasOne(e => e.Assessment)
                .WithMany(e => e.RubricItems)
                .HasForeignKey(e => e.AssessmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Submission>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.StudentId).HasMaxLength(100);
            entity.Property(e => e.StudentName).HasMaxLength(255);
            entity.Property(e => e.OriginalFileName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.FileType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.StoragePath).HasMaxLength(500).IsRequired();
            entity.Property(e => e.ExtractionStatus).HasMaxLength(50).IsRequired();
            entity.Property(e => e.GradingStatus).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ExtractionError).HasMaxLength(1000);

            entity.HasOne(e => e.Assessment)
                .WithMany(e => e.Submissions)
                .HasForeignKey(e => e.AssessmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GradingJob>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);

            entity.HasOne(e => e.Assessment)
                .WithMany(e => e.GradingJobs)
                .HasForeignKey(e => e.AssessmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GradingResult>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.AiModel).HasMaxLength(100);
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
            entity.Property(e => e.ReviewStatus).HasMaxLength(30).IsRequired();
            entity.Property(e => e.TeacherOverallComment).HasMaxLength(2000);

            // Cascade from Assessment; NOT from Submission to avoid multiple cascade paths
            entity.HasOne(e => e.Assessment)
                .WithMany()
                .HasForeignKey(e => e.AssessmentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Submission)
                .WithMany(e => e.GradingResults)
                .HasForeignKey(e => e.SubmissionId)
                .OnDelete(DeleteBehavior.NoAction);

            // NoAction: avoids multi-path cascade (Assessment→GradingJob→GradingResult
            // conflicts with Assessment→GradingResult). GradingJob is only deleted via
            // its Assessment cascade, at which point GradingResults are already removed.
            entity.HasOne(e => e.GradingJob)
                .WithMany(e => e.GradingResults)
                .HasForeignKey(e => e.GradingJobId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<GradingResultItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(255).IsRequired();
            // RubricItemId is a plain column — no FK, so rubric changes don't break history
            entity.Property(e => e.RubricItemId);
            entity.Property(e => e.TeacherComment).HasMaxLength(2000);

            entity.HasOne(e => e.GradingResult)
                .WithMany(e => e.Items)
                .HasForeignKey(e => e.GradingResultId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}