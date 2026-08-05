using FlowDesk.Constants;
using FlowDesk.DTOs.DepartmentManager;
using FlowDesk.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FlowDesk.Controllers
{
    [Authorize(Roles = AppRoles.DepartmentManager)]
    [ResponseCache(
        Duration = 0,
        Location = ResponseCacheLocation.None,
        NoStore = true)]
    public class DepartmentManagerController : Controller
    {
        private readonly IDepartmentManagerWorkflowService
            _departmentManagerWorkflowService;

        private readonly IApprovedWorkItemExcelService
            _approvedWorkItemExcelService;
        private readonly IAccountApprovalService
            _accountApprovalService;

        public DepartmentManagerController(
            IDepartmentManagerWorkflowService departmentManagerWorkflowService,
            IApprovedWorkItemExcelService approvedWorkItemExcelService,
            IAccountApprovalService accountApprovalService)
        {
            _departmentManagerWorkflowService =
                departmentManagerWorkflowService;

            _approvedWorkItemExcelService = approvedWorkItemExcelService;
            _accountApprovalService = accountApprovalService;
        }

        public IActionResult Index()
        {
            return RedirectToAction(nameof(Inbox));
        }

        [HttpGet]
        public async Task<IActionResult> Inbox()
        {
            var result =
                await _departmentManagerWorkflowService
                    .GetInboxAsync(GetCurrentUserId());

            if (result.IsForbidden)
            {
                return Forbid();
            }

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] =
                    result.ErrorMessage;

                return View();
            }

            return View(result.Data);
        }

        [HttpGet]
        public async Task<IActionResult> Review(int id)
        {
            var result =
                await _departmentManagerWorkflowService
                    .GetReviewAsync(id, GetCurrentUserId());

            if (result.IsNotFound)
            {
                return NotFound();
            }

            if (result.IsForbidden)
            {
                return Forbid();
            }

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] =
                    result.ErrorMessage;

                return RedirectToAction(nameof(Inbox));
            }

            return View(result.Data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(
            int id,
            ApproveRequestDto dto)
        {
            dto.WorkItemId = id;

            var result =
                await _departmentManagerWorkflowService
                    .ApproveRequestAsync(
                        dto,
                        GetCurrentUserId());

            if (result.IsNotFound)
            {
                return NotFound();
            }

            if (result.IsForbidden)
            {
                return Forbid();
            }

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] =
                    result.ErrorMessage;

                return RedirectToAction(
                    nameof(Review),
                    new { id });
            }

            if (!string.IsNullOrWhiteSpace(result.SuccessMessage))
            {
                TempData["WarningMessage"] = result.SuccessMessage;
            }
            else
            {
                TempData["SuccessMessage"] =
                    "Talep başarıyla onaylandı.";
            }

            return RedirectToAction(
                nameof(ApprovedRequests));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnToAnalyst(
            int id,
            ReturnToAnalystDto dto)
        {
            dto.WorkItemId = id;

            var result =
                await _departmentManagerWorkflowService
                    .ReturnToAnalystAsync(
                        dto,
                        GetCurrentUserId());

            if (result.IsNotFound)
            {
                return NotFound();
            }

            if (result.IsForbidden)
            {
                return Forbid();
            }

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] =
                    result.ErrorMessage;

                return RedirectToAction(
                    nameof(Review),
                    new { id });
            }

            TempData["SuccessMessage"] =
                "Talep analiste iade edildi.";

            return RedirectToAction(nameof(Inbox));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(
            int id,
            RejectRequestDto dto)
        {
            dto.WorkItemId = id;
            var result = await _departmentManagerWorkflowService
                .RejectRequestAsync(dto, GetCurrentUserId());

            if (result.IsNotFound)
            {
                return NotFound();
            }

            if (result.IsForbidden)
            {
                return Forbid();
            }

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] = result.ErrorMessage;
                return RedirectToAction(nameof(Review), new { id });
            }

            TempData["SuccessMessage"] = "Talep reddedildi.";
            return RedirectToAction(nameof(Inbox));
        }
        [HttpGet]
        public async Task<IActionResult> SharedExcel()
        {
            var result = await _departmentManagerWorkflowService
                .GetSharedExcelAsync(GetCurrentUserId());

            if (result.IsForbidden)
            {
                return Forbid();
            }

            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] = result.ErrorMessage;
                return View();
            }

            return View(result.Data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditApprovedWorkItem(
            int id,
            EditApprovedWorkItemDto dto)
        {
            dto.WorkItemId = id;
            var result = await _departmentManagerWorkflowService
                .EditApprovedWorkItemAsync(dto, GetCurrentUserId());

            if (result.IsNotFound)
            {
                return NotFound();
            }

            if (result.IsForbidden)
            {
                return Forbid();
            }

            if (result.IsConflict)
            {
                TempData["ErrorMessage"] = result.ErrorMessage;
                return RedirectToAction(nameof(SharedExcel));
            }

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] = result.ErrorMessage;
                return RedirectToAction(nameof(SharedExcel));
            }

            if (!string.IsNullOrWhiteSpace(result.SuccessMessage))
            {
                TempData["WarningMessage"] = result.SuccessMessage;
            }
            else
            {
                TempData["SuccessMessage"] =
                    "Değişiklikler kaydedildi.";
            }

            return RedirectToAction(nameof(SharedExcel));
        }

        [HttpGet]
        public async Task<IActionResult> DownloadSharedExcel(
            CancellationToken cancellationToken)
        {
            var result = await _approvedWorkItemExcelService.DownloadAsync(
                cancellationToken);
            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] = result.ErrorMessage;
                return RedirectToAction(nameof(SharedExcel));
            }

            return File(
                result.Data.Content,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                result.Data.FileName);
        }

        [HttpGet]
        public async Task<IActionResult> ApprovedRequests()
        {
            var result =
                await _departmentManagerWorkflowService
                    .GetApprovedRequestsAsync(GetCurrentUserId());

            if (result.IsForbidden)
            {
                return Forbid();
            }

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] =
                    result.ErrorMessage;

                return View();
            }

            return View(result.Data);
        }

        [HttpGet]
        public async Task<IActionResult> PendingUsers()
        {
            var result =
                await _accountApprovalService
                    .GetPendingUsersAsync(GetCurrentUserId());

            if (result.IsForbidden)
            {
                return Forbid();
            }

            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] = result.ErrorMessage;
                return View(Array.Empty<
                    FlowDesk.ViewModels.DepartmentManager.PendingUserViewModel>());
            }

            return View(result.Data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveUser(int id)
        {
            var result =
                await _accountApprovalService
                    .ApproveUserAsync(
                        id,
                        GetCurrentUserId());

            if (result.IsForbidden)
            {
                return Forbid();
            }

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] =
                    result.ErrorMessage;

                return RedirectToAction(nameof(PendingUsers));
            }

            TempData["SuccessMessage"] =
                result.SuccessMessage;

            return RedirectToAction(nameof(PendingUsers));
        }

        private int? GetCurrentUserId()
        {
            string? userIdValue = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            return int.TryParse(userIdValue, out int userId) && userId > 0
                ? userId
                : null;
        }
    }
}
