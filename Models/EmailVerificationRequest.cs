using System.ComponentModel.DataAnnotations;

namespace FlowDesk.Models
{
    public class EmailVerificationRequest
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public ApplicationUser User { get; set; } = null!;

        [Required]
        public string CodeHash { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; }

        public DateTime ExpiresAtUtc { get; set; }

        public int FailedAttemptCount { get; set; }

        public DateTime? VerifiedAtUtc { get; set; }

        public bool IsInvalidated { get; set; }
    }
}
