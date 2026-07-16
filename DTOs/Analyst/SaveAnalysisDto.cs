namespace FlowDesk.DTOs.Analyst
{
    public class SaveAnalysisDto
    {
        public int WorkItemId { get; set; }

        public int? AnalystId { get; set; }

        public int? DeveloperId { get; set; }

        public DateTime? ReleaseDate { get; set; }

        public DateTime? BanksoftDeliveryDate { get; set; }

        public string? ExpectedStatus { get; set; }

        public string? CurrentStatus { get; set; }

        public string? AnalystNote { get; set; }
    }
}