using System.ComponentModel.DataAnnotations;
using FlowDesk.Ai.Entities;

namespace FlowDesk.Ai.Models;

public sealed class SaveAnalystAiDraftRequest
{
    [Required]
    [StringLength(WorkItemAiDraft.RequestMaximumLength)]
    public string EditedRequest { get; init; } = string.Empty;

    [Required]
    public string RowVersion { get; init; } = string.Empty;
}
