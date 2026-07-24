using FlowDesk.Common;
using FlowDesk.Constants;
using FlowDesk.Models;
using FlowDesk.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FlowDesk.Controllers
{
    [Authorize(Roles = AppRoles.ProjectManager)]
    public class ProjectManagerController : Controller
    {
        private readonly IProjectManagerWorkItemService
            _workItemService;

        public ProjectManagerController(
            IProjectManagerWorkItemService workItemService)
        {
            _workItemService = workItemService;
        }

        // Proje yöneticisinin oluşturduğu talepleri listeler.
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ServiceResult<List<WorkItem>> result =
                await _workItemService.GetMyRequestsAsync(
                    GetCurrentUserId());

            return View(result.Data!);
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
            ServiceResult<string> validationResult =
                await _workItemService.ValidateCreateAsync(
                    workItem.RequestNumber);

            if (!validationResult.IsSuccess)
            {
                ModelState.AddModelError(
                    nameof(workItem.RequestNumber),
                    validationResult.ErrorMessage!
                );
            }

            ValidateDepartment(workItem);

            if (!ModelState.IsValid)
            {
                return View(workItem);
            }

            ServiceResult createResult =
                await _workItemService.CreateAsync(
                    workItem,
                    validationResult.Data!,
                    GetCurrentUserId());

            if (!createResult.IsSuccess)
            {
                ModelState.AddModelError(
                    string.Empty,
                    createResult.ErrorMessage!
                );
                return View(workItem);
            }

            TempData["SuccessMessage"] =
                "Talep başarıyla oluşturuldu ve analist incelemesine gönderildi.";

            return RedirectToAction(nameof(Index));
        }

        // Seçilen talebin detaylarını gösterir.
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            ServiceResult<WorkItem> result =
                await _workItemService.GetDetailsAsync(
                    id,
                    GetCurrentUserId());

            if (!result.IsSuccess)
            {
                return HandleWorkItemFailure(result);
            }

            return View(result.Data);
        }

        // Talebi güncelleme sayfasını açar.
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            ServiceResult<WorkItem> result =
                await _workItemService.GetForEditAsync(
                    id,
                    GetCurrentUserId());

            if (!result.IsSuccess)
            {
                return HandleWorkItemFailure(result);
            }

            return View(result.Data);
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

            ServiceResult<WorkItem> accessResult =
                await _workItemService.GetForEditAsync(
                    id,
                    GetCurrentUserId());

            if (!accessResult.IsSuccess)
            {
                return HandleWorkItemFailure(accessResult);
            }

            ServiceResult<string> validationResult =
                await _workItemService.ValidateUpdateAsync(
                    id,
                    workItem.RequestNumber);

            if (!validationResult.IsSuccess)
            {
                ModelState.AddModelError(
                    nameof(workItem.RequestNumber),
                    validationResult.ErrorMessage!
                );
            }

            ValidateDepartment(workItem);

            if (!ModelState.IsValid)
            {
                return View(workItem);
            }

            ServiceResult updateResult =
                await _workItemService.UpdateAsync(
                    id,
                    workItem,
                    validationResult.Data!,
                    GetCurrentUserId());

            if (!updateResult.IsSuccess)
            {
                return HandleWorkItemFailure(updateResult);
            }

            TempData["SuccessMessage"] =
                "Talep başarıyla güncellendi.";

            return RedirectToAction(nameof(Index));
        }

        // Talep silme onay sayfasını açar.
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            ServiceResult<WorkItem> result =
                await _workItemService.GetForDeleteAsync(
                    id,
                    GetCurrentUserId());

            if (!result.IsSuccess)
            {
                return HandleWorkItemFailure(result);
            }

            return View(result.Data);
        }

        // Talebi sistemden siler.
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            ServiceResult result =
                await _workItemService.DeleteAsync(
                    id,
                    GetCurrentUserId());

            if (!result.IsSuccess)
            {
                return HandleWorkItemFailure(result);
            }

            TempData["SuccessMessage"] =
                "Talep başarıyla silindi.";

            return RedirectToAction(nameof(Index));
        }

        private IActionResult HandleWorkItemFailure(
            ServiceResult result)
        {
            if (result.IsNotFound)
            {
                return NotFound();
            }

            if (result.IsForbidden)
            {
                return Forbid();
            }

            TempData["ErrorMessage"] = result.ErrorMessage;
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