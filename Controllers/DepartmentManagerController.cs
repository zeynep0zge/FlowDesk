using FlowDesk.Constants;
using FlowDesk.DTOs.DepartmentManager;
using FlowDesk.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

        public DepartmentManagerController(

            IDepartmentManagerWorkflowService departmentManagerWorkflowService,
            IExcelExportService excelExportService)
        {
            _departmentManagerWorkflowService =
                departmentManagerWorkflowService;

            _excelExportService = excelExportService;
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

    }
}