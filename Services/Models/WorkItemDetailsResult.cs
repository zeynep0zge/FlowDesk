using FlowDesk.Models;

namespace FlowDesk.Services.Models;

public sealed record WorkItemDetailsResult(
    int Id,
    string RequestNumber,
    string RequestDescription,
    string Department,
    RequestPriority Priority,
    WorkflowStatus WorkflowStatus,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? AnalystNote,
    string? ManagerNote,
    string? AnalystName,
    string? AnalystBusinessCode,
    string? DeveloperName,
    string? DeveloperBusinessCode);
