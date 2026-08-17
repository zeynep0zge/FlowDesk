using FlowDesk.Models;
using FlowDesk.Constants;

namespace FlowDesk.ViewModels.Analyst
{
    public class AnalystReviewViewModel
    {
        // Hangi talebin incelendiğini belirtir.
        public int Id { get; set; }

        // Proje yöneticisinin oluşturduğu, yalnızca gösterilecek alanlar.
        public string RequestNumber { get; set; } = string.Empty;

        public string RequestDescription { get; set; } = string.Empty;

        public string Department { get; set; } = string.Empty;

        public RequestPriority Priority { get; set; }

        public WorkflowStatus WorkflowStatus { get; set; }

        public DateTime CreatedAt { get; set; }

        // Analistin düzenleyebileceği alanlar.
        public int? AnalystId { get; set; }

        public string AnalystDisplayName { get; set; } = string.Empty;

        public int? DeveloperId { get; set; }

        public List<UserSelectionOptionViewModel> DeveloperOptions
            { get; set; } = [];

        public DateTime? ReleaseDate { get; set; }

        public DateTime? BanksoftDeliveryDate { get; set; }

        public string? ExpectedStatus { get; set; }

        public string? CurrentStatus { get; set; }

        public string? AnalystNote { get; set; }

        // Departman yöneticisi talebi iade ettiğinde gösterilecek not.
        public string? ManagerNote { get; set; }

        public List<FeedbackMessage> FeedbackMessages { get; set; } = [];

        // Mevcut statü seçim kutusunda gösterilecek seçenekler.
        public List<string> CurrentStatusOptions { get; set; } =
            [.. AnalystCurrentStatusOptions.All];
    }
}
