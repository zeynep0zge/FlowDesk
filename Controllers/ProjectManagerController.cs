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

            if (!result.IsSuccess)
            {
                return HandleWorkItemFailure(result);
            }

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
                "RequestDescription," +
                "Department," +
                "Priority"
            )]
            WorkItem workItem
        )
        {
            int? currentUserId = GetCurrentUserId();

            if (!currentUserId.HasValue)
            {
                return Forbid();
            }

            ModelState.Remove(nameof(WorkItem.RequestNumber));

            ValidateDepartment(workItem);

            if (!ModelState.IsValid)
            {
                return View(workItem);
            }

            ServiceResult createResult =
                await _workItemService.CreateAsync(
                    workItem,
                    currentUserId);

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendFeedbackMessage(
            int id,
            string? message)
        {
            ServiceResult result = await _workItemService
                .SendFeedbackMessageAsync(
                    id,
                    message,
                    GetCurrentUserId());

            if (result.IsNotFound)
            {
                return NotFound();
            }

            if (result.IsForbidden)
            {
                return Forbid();
            }

            TempData[result.IsSuccess
                ? "SuccessMessage"
                : "ErrorMessage"] = result.IsSuccess
                ? "Mesaj gönderildi."
                : result.ErrorMessage;

            return result.IsSuccess
                ? RedirectToAction("Index", "ProjectManager")
                : RedirectToAction(nameof(Details), new { id });
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
            [Bind("Id,RequestDescription,Department,Priority")]
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

            workItem.RequestNumber = accessResult.Data!.RequestNumber;
            ModelState.Remove(nameof(WorkItem.RequestNumber));

            ValidateDepartment(workItem);

            if (!ModelState.IsValid)
            {
                return View(workItem);
            }

            ServiceResult updateResult =
                await _workItemService.UpdateAsync(
                    id,
                    workItem,
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

            if (int.TryParse(userIdValue, out int userId) && userId > 0)
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
