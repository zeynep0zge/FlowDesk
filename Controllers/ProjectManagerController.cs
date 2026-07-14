using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Claims;
using FlowDesk.Data;
using FlowDesk.Models;

namespace FlowDesk.Controllers
{
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

            /*
             * Kullanıcı giriş sistemi eklendiğinde sadece giriş yapan
             * proje yöneticisinin talepleri gösterilecek.
             */

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

            if (!ModelState.IsValid)
            {
                return View(workItem);
            }

            // Proje yöneticisinin girdiği bilgiler
            workItem.RequestNumber = normalizedRequestNumber;
            workItem.RequestDescription =
                workItem.RequestDescription.Trim();

            workItem.Department = workItem.Department.Trim();

            // Kullanıcı giriş sistemi varsa kullanıcı ID'si alınır.
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

            // Login sistemi henüz yapılmadıysa null döner.
            return null;
        }
    }
}