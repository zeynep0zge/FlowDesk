using FlowDesk.Common;
using FlowDesk.Constants;
using FlowDesk.Data;
using FlowDesk.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Claims;

namespace FlowDesk.Controllers
{
    [Authorize(Roles = AppRoles.ProjectManager)]
    public class ProjectManagerController : Controller
    {
        private readonly AppDbContext _context;

        public ProjectManagerController(AppDbContext context)
        {
            _context = context;
        }

        // Proje yöneticisinin oluşturduğu talepleri listeler.
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            IQueryable<WorkItem> query = _context.WorkItems
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAt);

            // Kullanıcı kimliği mevcutsa listeyi giriş yapan proje yöneticisiyle sınırlar.

            int? currentUserId = GetCurrentUserId();

            if (currentUserId.HasValue)
            {
                query = query.Where(
                    x => x.CreatedByUserId == currentUserId.Value
                );
            }

            List<WorkItem> workItems = await query.ToListAsync();

            return View(workItems);
        }

        // Yeni talep oluşturma sayfasını açar.
        [HttpGet]
        public IActionResult Create()
        {
            return View(new WorkItem
            {
                Priority = RequestPriority.Normal
            });
        }

        // Formdan gelen yeni talebi kaydeder.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind(
                "RequestNumber," +
                "RequestDescription," +
                "Department," +
                "Priority"
            )]
            WorkItem workItem
        )
        {
            string normalizedRequestNumber =
                workItem.RequestNumber.Trim();

            bool requestNumberExists =
                await _context.WorkItems.AnyAsync(
                    x => x.RequestNumber == normalizedRequestNumber
                );

            if (requestNumberExists)
            {
                ModelState.AddModelError(
                    nameof(workItem.RequestNumber),
                    "Bu talep numarası daha önce kullanılmış."
                );
            }

            ValidateDepartment(workItem);

            if (!ModelState.IsValid)
            {
                return View(workItem);
            }

            // Proje yöneticisinin girdiği bilgiler
            workItem.RequestNumber = normalizedRequestNumber;
            workItem.RequestDescription =
                workItem.RequestDescription.Trim();

            workItem.Department = workItem.Department.Trim();

            // Talep, giriş yapan kullanıcının kimliğiyle ilişkilendirilir.
            workItem.CreatedByUserId = GetCurrentUserId();

            // Sistem tarafından başlangıç değerleri atanır.
            workItem.WorkflowStatus = WorkflowStatus.Submitted;
            workItem.CurrentStatus =
                "Analist İncelemesi Bekliyor";

            workItem.CreatedAt = DateTime.UtcNow;
            workItem.UpdatedAt = null;
            workItem.ApprovedAt = null;

            // Henüz analist ve yazılımcı atanmamıştır.
            workItem.AnalystId = null;
            workItem.DeveloperId = null;

            // Bu alanlar analist tarafından doldurulacaktır.
            workItem.ReleaseDate = null;
            workItem.BanksoftDeliveryDate = null;
            workItem.ExpectedStatus = null;
            workItem.AnalystNote = null;

            // Bu alan yönetici kontrolünde doldurulacaktır.
            workItem.ManagerNote = null;

            _context.WorkItems.Add(workItem);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Talep başarıyla oluşturuldu ve analist incelemesine gönderildi.";

            return RedirectToAction(nameof(Index));
        }

        // Seçilen talebin detaylarını gösterir.
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            WorkItem? workItem = await _context.WorkItems
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (workItem == null)
            {
                return NotFound();
            }

            int? currentUserId = GetCurrentUserId();

            /*
             * Kullanıcı giriş yaptıysa başka proje yöneticisine ait
             * talebi görüntülemesine izin verilmez.
             */
            if (
                currentUserId.HasValue &&
                workItem.CreatedByUserId.HasValue &&
                workItem.CreatedByUserId != currentUserId
            )
            {
                return Forbid();
            }

            return View(workItem);
        }

        // Talebi güncelleme sayfasını açar.
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            WorkItem? workItem = await _context.WorkItems
                .FirstOrDefaultAsync(x => x.Id == id);

            if (workItem == null)
            {
                return NotFound();
            }

            int? currentUserId = GetCurrentUserId();
            if (
                currentUserId.HasValue &&
                workItem.CreatedByUserId.HasValue &&
                workItem.CreatedByUserId != currentUserId
            )
            {
                return Forbid();
            }

            if (workItem.WorkflowStatus != WorkflowStatus.Submitted)
            {
                TempData["ErrorMessage"] = "Sadece 'Gönderildi' durumundaki talepler güncellenebilir.";
                return RedirectToAction(nameof(Index));
            }

            return View(workItem);
        }

        // Güncellenen talebi kaydeder.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("Id,RequestNumber,RequestDescription,Department,Priority")]
            WorkItem workItem
        )
        {
            if (id != workItem.Id)
            {
                return BadRequest();
            }

            WorkItem? dbWorkItem = await _context.WorkItems
                .FirstOrDefaultAsync(x => x.Id == id);

            if (dbWorkItem == null)
            {
                return NotFound();
            }

            int? currentUserId = GetCurrentUserId();
            if (
                currentUserId.HasValue &&
                dbWorkItem.CreatedByUserId.HasValue &&
                dbWorkItem.CreatedByUserId != currentUserId
            )
            {
                return Forbid();
            }

            if (dbWorkItem.WorkflowStatus != WorkflowStatus.Submitted)
            {
                TempData["ErrorMessage"] = "Sadece 'Gönderildi' durumundaki talepler güncellenebilir.";
                return RedirectToAction(nameof(Index));
            }

            string normalizedRequestNumber = workItem.RequestNumber.Trim();
            bool requestNumberExists = await _context.WorkItems.AnyAsync(
                x => x.RequestNumber == normalizedRequestNumber && x.Id != id
            );

            if (requestNumberExists)
            {
                ModelState.AddModelError(
                    nameof(workItem.RequestNumber),
                    "Bu talep numarası daha önce kullanılmış."
                );
            }

            ValidateDepartment(workItem);

            if (!ModelState.IsValid)
            {
                return View(workItem);
            }

            dbWorkItem.RequestNumber = normalizedRequestNumber;
            dbWorkItem.RequestDescription = workItem.RequestDescription.Trim();
            dbWorkItem.Department = workItem.Department.Trim();
            dbWorkItem.Priority = workItem.Priority;
            dbWorkItem.UpdatedAt = DateTime.UtcNow;

            _context.Update(dbWorkItem);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Talep başarıyla güncellendi.";
            return RedirectToAction(nameof(Index));
        }

        // Talep silme onay sayfasını açar.
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            WorkItem? workItem = await _context.WorkItems
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (workItem == null)
            {
                return NotFound();
            }

            int? currentUserId = GetCurrentUserId();
            if (
                currentUserId.HasValue &&
                workItem.CreatedByUserId.HasValue &&
                workItem.CreatedByUserId != currentUserId
            )
            {
                return Forbid();
            }

            if (workItem.WorkflowStatus != WorkflowStatus.Submitted)
            {
                TempData["ErrorMessage"] = "Sadece 'Gönderildi' durumundaki talepler silinebilir.";
                return RedirectToAction(nameof(Index));
            }

            return View(workItem);
        }

        // Talebi sistemden siler.
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            WorkItem? workItem = await _context.WorkItems
                .FirstOrDefaultAsync(x => x.Id == id);

            if (workItem == null)
            {
                return NotFound();
            }

            int? currentUserId = GetCurrentUserId();
            if (
                currentUserId.HasValue &&
                workItem.CreatedByUserId.HasValue &&
                workItem.CreatedByUserId != currentUserId
            )
            {
                return Forbid();
            }

            if (workItem.WorkflowStatus != WorkflowStatus.Submitted)
            {
                TempData["ErrorMessage"] = "Sadece 'Gönderildi' durumundaki talepler silinebilir.";
                return RedirectToAction(nameof(Index));
            }

            _context.WorkItems.Remove(workItem);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Talep başarıyla silindi.";
            return RedirectToAction(nameof(Index));
        }

        // Giriş yapan kullanıcının ID bilgisini alır.
        private int? GetCurrentUserId()
        {
            string? userIdValue = User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

            if (int.TryParse(userIdValue, out int userId))
            {
                return userId;
            }

            // Geçerli bir kullanıcı kimliği claim'i yoksa null döner.
            return null;
        }

        private void ValidateDepartment(WorkItem workItem)
        {
            if (!string.IsNullOrWhiteSpace(workItem.Department) &&
                !DepartmentOptions.Contains(workItem.Department))
            {
                ModelState.AddModelError(
                    nameof(workItem.Department),
                    "Geçerli bir departman seçiniz."
                );
            }
        }
    }
}