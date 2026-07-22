using FlowDesk.Models;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.Controllers
{
    public class EmployeeController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return View(Array.Empty<WorkItem>());
        }
    }
}
