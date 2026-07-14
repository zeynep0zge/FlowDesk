using FlowDesk.Data;
using FlowDesk.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Controllers
{
    public class AnalystController : Controller
    {
        private readonly AppDbContext _context;

        public AnalystController(AppDbContext context)
        {
            _context = context;
        }

        // /Analyst adresine gidildiğinde gelen talepleri açar.
        [HttpGet]
        public IActionResult Index()
        {
            return RedirectToAction(nameof(Inbox));
        }

        // Analistin inceleyebileceği talepleri listeler.
        [HttpGet]
        public async Task<IActionResult> Inbox()
        {
            List<WorkItem> workItems = await _context.WorkItems
                .AsNoTracking()
                .Where(x =>
                    x.WorkflowStatus == WorkflowStatus.Submitted ||
                    x.WorkflowStatus == WorkflowStatus.UnderAnalystReview
                )
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            int returnedToAnalystCount = await _context.WorkItems
                .AsNoTracking()
                .CountAsync(x =>
                    x.WorkflowStatus == WorkflowStatus.ReturnedToAnalyst
                );

            ViewData["ReturnedToAnalystCount"] = returnedToAnalystCount;

            return View(workItems);
        }

        // Analistin seçilen talebi inceleme ekranını açar.
        [HttpGet]
        public async Task<IActionResult> Review(int id)
        {
            WorkItem? workItem = await _context.WorkItems
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (workItem == null)
            {
                return NotFound();
            }

            if (!CanAnalystEdit(workItem.WorkflowStatus))
            {
                TempData["ErrorMessage"] =
                    "Bu talep analist tarafından düzenlenebilecek aşamada değildir.";

                return RedirectToAction(nameof(Inbox));
            }

            return View(workItem);
        }

        // Yeni talepleri POST ile analist incelemesine alır.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartReview(int id)
        {
            WorkItem? workItem = await _context.WorkItems
                .FirstOrDefaultAsync(x => x.Id == id);

            if (workItem == null)
            {
                return NotFound();
            }

            if (workItem.WorkflowStatus == WorkflowStatus.Submitted)
            {
                workItem.WorkflowStatus =
                    WorkflowStatus.UnderAnalystReview;

                workItem.CurrentStatus =
                    "Analist İncelemesinde";

                workItem.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(
                nameof(Review),
                new { id = workItem.Id }
            );
        }

        // Analistin yaptığı inceleme ve atamaları taslak olarak kaydeder.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAnalysis(
            int id,
            int? analystId,
            int? developerId,
            DateTime? releaseDate,
            DateTime? banksoftDeliveryDate,
            string? expectedStatus,
            string? currentStatus,
            string? analystNote)
        {
            string? normalizedExpectedStatus =
                NormalizeNullableText(expectedStatus);

            string? normalizedCurrentStatus =
                NormalizeNullableText(currentStatus);

            string? normalizedAnalystNote =
                NormalizeNullableText(analystNote);

            WorkItem? workItem = await _context.WorkItems
                .FirstOrDefaultAsync(x => x.Id == id);

            if (workItem == null)
            {
                return NotFound();
            }

            if (!CanAnalystEdit(workItem.WorkflowStatus))
            {
                TempData["ErrorMessage"] =
                    "Bu talep analist tarafından düzenlenebilecek aşamada değildir.";

                return RedirectToAction(nameof(Inbox));
            }

            if (analystId.HasValue && analystId.Value <= 0)
            {
                ModelState.AddModelError(
                    nameof(analystId),
                    "Analist ID 0'dan büyük olmalıdır."
                );
            }

            if (developerId.HasValue && developerId.Value <= 0)
            {
                ModelState.AddModelError(
                    nameof(developerId),
                    "Yazılımcı ID 0'dan büyük olmalıdır."
                );
            }

            if (
                releaseDate.HasValue &&
                banksoftDeliveryDate.HasValue &&
                banksoftDeliveryDate.Value.Date > releaseDate.Value.Date
            )
            {
                ModelState.AddModelError(
                    nameof(banksoftDeliveryDate),
                    "Banksoft teslim tarihi, sürüm tarihinden sonra olamaz."
                );
            }

            if (
                normalizedAnalystNote != null &&
                normalizedAnalystNote.Length > 1000
            )
            {
                ModelState.AddModelError(
                    nameof(analystNote),
                    "Analist notu en fazla 1000 karakter olabilir."
                );
            }

            // Analistin girdiği bilgiler
            workItem.AnalystId = analystId;
            workItem.DeveloperId = developerId;
            workItem.ReleaseDate = releaseDate;
            workItem.BanksoftDeliveryDate = banksoftDeliveryDate;
            workItem.ExpectedStatus = normalizedExpectedStatus;
            workItem.AnalystNote = normalizedAnalystNote;
            workItem.CurrentStatus =
                normalizedCurrentStatus ?? "Analist İncelemesinde";

            if (!ModelState.IsValid)
            {
                return View("Review", workItem);
            }

            workItem.WorkflowStatus =
                WorkflowStatus.UnderAnalystReview;

            workItem.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Analiz ve atama bilgileri kaydedildi.";

            return RedirectToAction(
                nameof(Review),
                new { id = workItem.Id }
            );
        }

        // Analistin çalışmasını departman yöneticisinin onayına gönderir.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitForApproval(int id)
        {
            WorkItem? workItem = await _context.WorkItems
                .FirstOrDefaultAsync(x => x.Id == id);

            if (workItem == null)
            {
                return NotFound();
            }

            if (
                workItem.WorkflowStatus ==
                WorkflowStatus.WaitingManagerApproval
            )
            {
                TempData["ErrorMessage"] =
                    "Bu talep zaten departman yöneticisi onayına gönderilmiş.";

                return RedirectToAction(nameof(Inbox));
            }

            if (!CanAnalystEdit(workItem.WorkflowStatus))
            {
                TempData["ErrorMessage"] =
                    "Bu talep yönetici onayına gönderilebilecek aşamada değildir.";

                return RedirectToAction(nameof(Inbox));
            }

            workItem.ExpectedStatus =
                NormalizeNullableText(workItem.ExpectedStatus);

            List<string> missingFields = new();
            List<string> invalidFields = new();

            if (!workItem.AnalystId.HasValue)
            {
                missingFields.Add("analist");
            }
            else if (workItem.AnalystId.Value <= 0)
            {
                invalidFields.Add("analist ID");
            }

            if (!workItem.DeveloperId.HasValue)
            {
                missingFields.Add("yazılımcı");
            }
            else if (workItem.DeveloperId.Value <= 0)
            {
                invalidFields.Add("yazılımcı ID");
            }

            if (!workItem.ReleaseDate.HasValue)
            {
                missingFields.Add("sürüm tarihi");
            }

            if (!workItem.BanksoftDeliveryDate.HasValue)
            {
                missingFields.Add("Banksoft teslim tarihi");
            }

            if (string.IsNullOrWhiteSpace(workItem.ExpectedStatus))
            {
                missingFields.Add("beklenen statü");
            }

            if (missingFields.Count > 0)
            {
                TempData["ErrorMessage"] =
                    "Yönetici onayına göndermeden önce şu alanları doldurun: " +
                    string.Join(", ", missingFields) +
                    ".";

                return RedirectToAction(
                    nameof(Review),
                    new { id = workItem.Id }
                );
            }

            if (invalidFields.Count > 0)
            {
                TempData["ErrorMessage"] =
                    "Şu alanlar 0'dan büyük olmalıdır: " +
                    string.Join(", ", invalidFields) +
                    ".";

                return RedirectToAction(
                    nameof(Review),
                    new { id = workItem.Id }
                );
            }

            DateTime releaseDateForApproval =
                workItem.ReleaseDate.GetValueOrDefault();

            DateTime banksoftDeliveryDateForApproval =
                workItem.BanksoftDeliveryDate.GetValueOrDefault();

            if (
                banksoftDeliveryDateForApproval.Date >
                releaseDateForApproval.Date
            )
            {
                TempData["ErrorMessage"] =
                    "Banksoft teslim tarihi, sürüm tarihinden sonra olamaz.";

                return RedirectToAction(
                    nameof(Review),
                    new { id = workItem.Id }
                );
            }

            workItem.WorkflowStatus =
                WorkflowStatus.WaitingManagerApproval;

            workItem.CurrentStatus =
                "Departman Yöneticisi Onayı Bekliyor";

            workItem.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Talep departman yöneticisinin onayına gönderildi.";

            return RedirectToAction(nameof(Inbox));
        }

        // Departman yöneticisinin analiste geri gönderdiği talepleri listeler.
        [HttpGet]
        public async Task<IActionResult> ReturnedRequests()
        {
            List<WorkItem> returnedWorkItems =
                await _context.WorkItems
                    .AsNoTracking()
                    .Where(x =>
                        x.WorkflowStatus ==
                        WorkflowStatus.ReturnedToAnalyst
                    )
                    .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                    .ToListAsync();

            return View(returnedWorkItems);
        }

        private static bool CanAnalystEdit(
            WorkflowStatus workflowStatus)
        {
            return workflowStatus == WorkflowStatus.Submitted ||
                   workflowStatus == WorkflowStatus.UnderAnalystReview ||
                   workflowStatus == WorkflowStatus.ReturnedToAnalyst;
        }

        private static string? NormalizeNullableText(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }
    }
}
