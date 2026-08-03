using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using FlowDesk.Models;
using FlowDesk.Ai.Entities;

namespace FlowDesk.Data
{
    public class AppDbContext
        : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }
        public DbSet<PasswordResetRequest> PasswordResetRequests { get; set; }
        public DbSet<EmailVerificationRequest> EmailVerificationRequests { get; set; }
        public DbSet<WorkItem> WorkItems { get; set; }
        public DbSet<WorkItemAiDraft> WorkItemAiDrafts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<PasswordResetRequest>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PasswordResetRequest>()
                .HasIndex(x => new
                {
                    x.UserId,
                    x.ExpiresAtUtc
                });
            modelBuilder.Entity<EmailVerificationRequest>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EmailVerificationRequest>()
                .HasIndex(x => new
                {
                    x.UserId,
                    x.ExpiresAtUtc
                });

            modelBuilder.Entity<ApplicationUser>()
                .Property(x => x.IsApproved)
                .HasDefaultValue(true);

            modelBuilder.Entity<ApplicationUser>()
                .HasIndex(x => x.BusinessCode)
                .IsUnique()
                .HasFilter("[BusinessCode] IS NOT NULL");

            modelBuilder.Entity<WorkItem>()

                .HasIndex(x => x.RequestNumber)

                .IsUnique();

            modelBuilder.Entity<WorkItemAiDraft>(entity =>
            {
                entity.ToTable("WorkItemAiDrafts");
                entity.HasKey(draft => draft.Id);

                entity.Property(draft => draft.GeneratedRequest)
                    .IsRequired()
                    .HasMaxLength(WorkItemAiDraft.RequestMaximumLength);
                entity.Property(draft => draft.EditedRequest)
                    .IsRequired()
                    .HasMaxLength(WorkItemAiDraft.RequestMaximumLength);
                entity.Property(draft => draft.AbbreviationsJson)
                    .IsRequired();
                entity.Property(draft => draft.AmbiguitiesJson)
                    .IsRequired();
                entity.Property(draft => draft.UnresolvedTermsJson)
                    .IsRequired();
                entity.Property(draft => draft.ModelName)
                    .IsRequired()
                    .HasMaxLength(WorkItemAiDraft.ModelNameMaximumLength);
                entity.Property(draft => draft.GeneratedAt)
                    .IsRequired();
                entity.Property(draft => draft.UpdatedAt)
                    .IsRequired();
                entity.Property(draft => draft.RowVersion)
                    .IsRowVersion()
                    .IsRequired();

                entity.HasIndex(draft => draft.WorkItemId)
                    .IsUnique();
                entity.HasOne(draft => draft.WorkItem)
                    .WithMany()
                    .HasForeignKey(draft => draft.WorkItemId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

        }
    }
}
