using System.ComponentModel.DataAnnotations;
using FlowDesk.Constants;

namespace FlowDesk.Models
{
    public enum WorkflowStatus
    {
        Submitted = 1,              // Proje yöneticisi talebi oluşturdu
        UnderAnalystReview = 2,     // Analist talebi inceliyor
        WaitingManagerApproval = 3, // Yönetici onayı bekleniyor
        ReturnedToAnalyst = 4,      // Yönetici analiste geri gönderdi
        Approved = 5,               // Yönetici onayladı
        Assigned = 6,               // Görev yazılımcıya atandı
        InProgress = 7,             // Görev üzerinde çalışılıyor
        Completed = 8,              // Görev tamamlandı
        Rejected = 9,               // Görev reddedildi
        ReturnedToBusinessUnit = 10 // Analist İş Birimine iade etti
    }

    public enum RequestPriority
    {
        Low = 1,
        Normal = 2,
        High = 3,
        Critical = 4
    }

    public class WorkItem
    {
        public int Id { get; set; }

        // Proje yöneticisinin girdiği alanlar

        [Required(ErrorMessage = "Talep numarası zorunludur.")]
        [StringLength(
            50,
            ErrorMessage = "Talep numarası en fazla 50 karakter olabilir."
        )]
        [Display(Name = "Talep No")]
        public string RequestNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Talep açıklaması zorunludur.")]
        [StringLength(
            2000,
            ErrorMessage = "Talep açıklaması en fazla 2000 karakter olabilir."
        )]
        [Display(Name = "Talep")]
        public string RequestDescription { get; set; } = string.Empty;

        [Required(ErrorMessage = "Departman seçilmelidir.")]
        [StringLength(100)]
        [Display(Name = "İlgili Departman")]
        public string Department { get; set; } = string.Empty;

        [Display(Name = "Öncelik")]
        public RequestPriority Priority { get; set; }
            = RequestPriority.Normal;

        // Talebi oluşturan proje yöneticisi

        [Display(Name = "Talebi Oluşturan Kullanıcı")]
        public int? CreatedByUserId { get; set; }

        // Analistin belirleyeceği alanlar

        [Display(Name = "Analist")]
        public int? AnalystId { get; set; }

        [Display(Name = "Yazılımcı")]
        public int? DeveloperId { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Sürüm Tarihi")]
        public DateTime? ReleaseDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Banksoft Teslim Tarihi")]
        public DateTime? BanksoftDeliveryDate { get; set; }

        [StringLength(100)]
        [Display(Name = "Beklenen Statü")]
        public string? ExpectedStatus { get; set; }

        [StringLength(AnalystCurrentStatusOptions.MaximumLength)]
        [Display(Name = "Mevcut Statü")]
        public string CurrentStatus { get; set; }
            = WorkflowStatusDescriptions.GetDescription(
                WorkflowStatus.Submitted);

        [StringLength(1000)]
        [Display(Name = "Analist Notu")]
        public string? AnalystNote { get; set; }

        [Display(Name = "Son Analist Geri Bildirimi")]
        public WorkflowStatus? AnalystFeedbackStatus { get; set; }

        [StringLength(500)]
        [Display(Name = "Analist Geri Bildirim Açıklaması")]
        public string? AnalystFeedbackDescription { get; set; }

        [Display(Name = "Analist Geri Bildirim Tarihi")]
        public DateTime? AnalystFeedbackAt { get; set; }

        public ICollection<FeedbackMessage> FeedbackMessages { get; set; }
            = new List<FeedbackMessage>();

        // Departman yöneticisinin belirleyeceği alanlar

        [StringLength(1000)]
        [Display(Name = "Departman Yöneticisi Notu")]
        public string? ManagerNote { get; set; }

        // Sistem tarafından yönetilecek alanlar

        [Display(Name = "Workflow Durumu")]
        public WorkflowStatus WorkflowStatus { get; set; }
            = WorkflowStatus.Submitted;

        [Display(Name = "Oluşturulma Tarihi")]
        public DateTime CreatedAt { get; set; }

        [Display(Name = "Güncellenme Tarihi")]
        public DateTime? UpdatedAt { get; set; }

        [Display(Name = "Onaylanma Tarihi")]
        public DateTime? ApprovedAt { get; set; }

        // Aynı kayıt iki kullanıcı tarafından eş zamanlı
        // değiştirilirse çakışmayı tespit etmek için kullanılacak.
        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
