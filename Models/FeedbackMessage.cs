using System.ComponentModel.DataAnnotations;

namespace FlowDesk.Models;

public sealed class FeedbackMessage
{
    public int Id { get; set; }

    public int WorkItemId { get; set; }

    public int SenderUserId { get; set; }

    [Required]
    [StringLength(2000)]
    public string Message { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }

    public WorkItem WorkItem { get; set; } = null!;

    public ApplicationUser SenderUser { get; set; } = null!;
}
