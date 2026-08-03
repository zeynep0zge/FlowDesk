using System.ComponentModel.DataAnnotations;
using FlowDesk.Models;

namespace FlowDesk.Ai.Entities;

public sealed class WorkItemAiDraft
{
    public const int RequestMaximumLength = 4000;
    public const int ModelNameMaximumLength = 100;

    public int Id { get; set; }

    public int WorkItemId { get; set; }

    [Required]
    [StringLength(RequestMaximumLength)]
    public string GeneratedRequest { get; set; } = string.Empty;

    [Required]
    [StringLength(RequestMaximumLength)]
    public string EditedRequest { get; set; } = string.Empty;

    [Required]
    public string AbbreviationsJson { get; set; } = "[]";

    [Required]
    public string AmbiguitiesJson { get; set; } = "[]";

    [Required]
    public string UnresolvedTermsJson { get; set; } = "[]";

    [Required]
    [StringLength(ModelNameMaximumLength)]
    public string ModelName { get; set; } = string.Empty;

    public DateTime GeneratedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    public WorkItem WorkItem { get; set; } = null!;
}
