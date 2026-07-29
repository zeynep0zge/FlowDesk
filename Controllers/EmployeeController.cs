using FlowDesk.Constants;
using FlowDesk.Models;
using FlowDesk.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FlowDesk.Controllers
{
    [Authorize(Roles = AppRoles.Employee)]
    public class EmployeeController : Controller
    {
        private readonly IEmployeeWorkItemService _employeeWorkItemService;

        public EmployeeController(
            IEmployeeWorkItemService employeeWorkItemService)
        {
            _employeeWorkItemService = employeeWorkItemService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var result = await _employeeWorkItemService
                .GetAssignedWorkItemsAsync(GetCurrentUserId());

            if (result.IsForbidden)
            {
                return Forbid();
            }

            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] = result.ErrorMessage;
                return View(new List<WorkItem>());
            }

            return View(result.Data);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var result = await _employeeWorkItemService.GetDetailsAsync(
                id,
                GetCurrentUserId());

            if (result.IsForbidden)
            {
                return Forbid();
            }

            if (result.IsNotFound || result.Data == null)
            {
                return NotFound();
            }

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] = result.ErrorMessage;
                return RedirectToAction(nameof(Index));
            }

            return View(result.Data);
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
