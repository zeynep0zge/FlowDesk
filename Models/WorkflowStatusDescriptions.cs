namespace FlowDesk.Models;

public static class WorkflowStatusDescriptions
{
    public static string GetDescription(WorkflowStatus status)
    {
        return status switch
        {
            WorkflowStatus.Submitted =>
                "Analist İncelemesi Bekliyor",
            WorkflowStatus.UnderAnalystReview =>
                "Analist İncelemesinde",
            WorkflowStatus.WaitingManagerApproval =>
                "Departman Yöneticisi Onayı Bekliyor",
            WorkflowStatus.ReturnedToAnalyst =>
                "Analiste İade Edildi",
            WorkflowStatus.Approved =>
                "Departman Yöneticisi Tarafından Onaylandı",
            WorkflowStatus.Assigned =>
                "Yazılımcıya Atandı",
            WorkflowStatus.InProgress =>
                "Geliştiriliyor",
            WorkflowStatus.Completed =>
                "Tamamlandı",
            WorkflowStatus.Rejected =>
                "Reddedildi",
            _ => status.ToString()
        };
    }
}