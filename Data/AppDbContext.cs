using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using FlowDesk.Models;

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

            modelBuilder.Entity<WorkItem>()

                .HasIndex(x => x.RequestNumber)

                .IsUnique();
        }
    }
}