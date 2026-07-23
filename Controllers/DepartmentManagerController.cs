using FlowDesk.Constants;
using FlowDesk.DTOs.DepartmentManager;
using FlowDesk.Models;
using FlowDesk.ViewModels.DepartmentManager;
using FlowDesk.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        private readonly IExcelExportService _excelExportService;
        private readonly UserManager<ApplicationUser> _userManager;

        public DepartmentManagerController(

            IDepartmentManagerWorkflowService departmentManagerWorkflowService,
            IExcelExportService excelExportService,
            UserManager<ApplicationUser> userManager)
        {
            _departmentManagerWorkflowService =
                departmentManagerWorkflowService;

            _excelExportService = excelExportService;
            _userManager = userManager;
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
                    .GetInboxAsync();

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
                    .GetReviewAsync(id);

            if (result.IsNotFound)
            {
                return NotFound();
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
                    .ApproveRequestAsync(dto);

            if (result.IsNotFound)
            {
                return NotFound();
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
                "Talep başarıyla onaylandı.";

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
                    .ReturnToAnalystAsync(dto);

            if (result.IsNotFound)
            {
                return NotFound();
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
        [HttpGet]
        public async Task<IActionResult> DownloadExcel(int id)
        {
            var result =
                await _departmentManagerWorkflowService
                    .GetApprovedRequestForExportAsync(id);

            if (result.IsNotFound)
            {
                return NotFound();
            }

            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] = result.ErrorMessage;

                return RedirectToAction(nameof(ApprovedRequests));
            }

            byte[] excelFile =
                _excelExportService
                    .CreateApprovedRequestExcel(result.Data);

            string safeRequestNumber =
                string.Join(
                    "_",
                    result.Data.RequestNumber.Split(
                        Path.GetInvalidFileNameChars(),
                        StringSplitOptions.RemoveEmptyEntries));

            string fileName =
                $"FlowDesk_{safeRequestNumber}.xlsx";

            return File(
                excelFile,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        [HttpGet]
        public async Task<IActionResult> ApprovedRequests()
        {
            var result =
                await _departmentManagerWorkflowService
                    .GetApprovedRequestsAsync();

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
            var pendingUsers = await _userManager.Users
                .AsNoTracking()
                .Where(user =>
                    user.EmailConfirmed &&
                    !user.IsApproved &&
                    user.RequestedRole != null &&
                    user.RequestedRole != string.Empty)
                .OrderBy(user => user.CreatedAtUtc)
                .Select(user => new PendingUserViewModel
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Email = user.Email ?? string.Empty,
                    Department = user.Department ?? string.Empty,
                    RequestedRole = user.RequestedRole ?? string.Empty,
                    CreatedAtUtc = user.CreatedAtUtc
                })
                .ToListAsync();

            return View(pendingUsers);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveUser(int id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());

            if (user == null)
            {
                TempData["ErrorMessage"] = "Kullanıcı bulunamadı.";
                return RedirectToAction(nameof(PendingUsers));
            }

            if (user.IsApproved)
            {
                TempData["SuccessMessage"] =
                    "Bu kullanıcı daha önce onaylanmış.";

                return RedirectToAction(nameof(PendingUsers));
            }

            if (!user.EmailConfirmed ||
                !AppRoles.IsSelfRegistrable(user.RequestedRole))
            {
                TempData["ErrorMessage"] =
                    "Kullanıcının e-posta veya rol talebi onay için uygun değil.";

                return RedirectToAction(nameof(PendingUsers));
            }

            bool alreadyInRole = await _userManager.IsInRoleAsync(
                user,
                user.RequestedRole!);

            if (!alreadyInRole)
            {
                var roleResult = await _userManager.AddToRoleAsync(
                    user,
                    user.RequestedRole!);

                if (!roleResult.Succeeded)
                {
                    TempData["ErrorMessage"] = string.Join(
                        " ",
                        roleResult.Errors.Select(error => error.Description));

                    return RedirectToAction(nameof(PendingUsers));
                }
            }

            user.IsApproved = true;
            var updateResult = await _userManager.UpdateAsync(user);

            if (!updateResult.Succeeded)
            {
                TempData["ErrorMessage"] = string.Join(
                    " ",
                    updateResult.Errors.Select(error => error.Description));

                return RedirectToAction(nameof(PendingUsers));
            }

            TempData["SuccessMessage"] =
                "Kullanıcı hesabı onaylandı ve talep edilen rol atandı.";

            return RedirectToAction(nameof(PendingUsers));
        }

    }
}