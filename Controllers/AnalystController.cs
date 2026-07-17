using FlowDesk.DTOs.Analyst;
using FlowDesk.Services.Interfaces;
using FlowDesk.ViewModels.Analyst;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.Controllers
{
    public class AnalystController : Controller
    {
        private readonly IAnalystWorkflowService
            _analystWorkflowService;

        public AnalystController(
            IAnalystWorkflowService analystWorkflowService)
        {
            _analystWorkflowService = analystWorkflowService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return RedirectToAction(nameof(Inbox));
        }

        [HttpGet]
        [ResponseCache(
            Duration = 0,
            Location = ResponseCacheLocation.None,
            NoStore = true)]
        public async Task<IActionResult> Inbox()
        {
            var result =
                await _analystWorkflowService.GetInboxAsync();

            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] =
                    result.ErrorMessage ??
                    "Analist gelen kutusu yüklenemedi.";

                return View(new AnalystInboxViewModel());
            }

            return View(result.Data);
        }

        [HttpGet]
        public async Task<IActionResult> Review(int id)
        {
            var result =
                await _analystWorkflowService.GetReviewAsync(id);

            if (result.IsNotFound)
            {
                return NotFound();
            }

            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] =
                    result.ErrorMessage ??
                    "Talep inceleme ekranı açılamadı.";

                return RedirectToAction(nameof(Inbox));
            }

            return View(result.Data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartReview(int id)
        {
            var result =
                await _analystWorkflowService
                    .StartReviewAsync(id);

            if (result.IsNotFound)
            {
                return NotFound();
            }

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] =
                    result.ErrorMessage ??
                    "Talep incelemeye alınamadı.";

                return RedirectToAction(nameof(Inbox));
            }

            return RedirectToAction(
                nameof(Review),
                new { id }
            );
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAnalysis(
            int id,
            SaveAnalysisDto dto)
        {
            dto.WorkItemId = id;

            var result =
                await _analystWorkflowService
                    .SaveAnalysisAsync(dto);

            if (result.IsNotFound)
            {
                return NotFound();
            }

            if (!result.IsSuccess)
            {
                ModelState.AddModelError(
                    string.Empty,
                    result.ErrorMessage ??
                    "Analiz bilgileri kaydedilemedi."
                );

                var reviewResult =
                    await _analystWorkflowService
                        .GetReviewAsync(id);

                if (reviewResult.IsNotFound)
                {
                    return NotFound();
                }

                if (!reviewResult.IsSuccess ||
                    reviewResult.Data == null)
                {
                    TempData["ErrorMessage"] =
                        reviewResult.ErrorMessage ??
                        "Talep bilgileri yüklenemedi.";

                    return RedirectToAction(nameof(Inbox));
                }

                ApplyPostedValues(
                    reviewResult.Data,
                    dto
                );

                return View(
                    "Review",
                    reviewResult.Data
                );
            }

            TempData["SuccessMessage"] =
                "Analiz ve atama bilgileri kaydedildi.";

            return RedirectToAction(
                nameof(Review),
                new { id }
            );
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitForApproval(
            int id)
        {
            SubmitForApprovalDto dto = new()
            {
                WorkItemId = id
            };

            var result =
                await _analystWorkflowService
                    .SubmitForApprovalAsync(dto);

            if (result.IsNotFound)
            {
                return NotFound();
            }

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] =
                    result.ErrorMessage ??
                    "Talep yönetici onayına gönderilemedi.";

                var reviewResult =
                    await _analystWorkflowService
                        .GetReviewAsync(id);

                if (reviewResult.IsSuccess)
                {
                    return RedirectToAction(
                        nameof(Review),
                        new { id }
                    );
                }

                return RedirectToAction(nameof(Inbox));
            }

            TempData["SuccessMessage"] =
                "Talep departman yöneticisinin " +
                "onayına gönderildi.";

            return RedirectToAction(nameof(Inbox));
        }

        [HttpGet]
        public async Task<IActionResult> ReturnedRequests()
        {
            var result =
                await _analystWorkflowService
                    .GetReturnedRequestsAsync();

            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] =
                    result.ErrorMessage ??
                    "İade edilen talepler yüklenemedi.";

                return View(
                    new List<AnalystInboxItemViewModel>()
                );
            }

            return View(result.Data);
        }

        private static void ApplyPostedValues(
            AnalystReviewViewModel viewModel,
            SaveAnalysisDto dto)
        {
            viewModel.AnalystId = dto.AnalystId;
            viewModel.DeveloperId = dto.DeveloperId;
            viewModel.ReleaseDate = dto.ReleaseDate;

            viewModel.BanksoftDeliveryDate =
                dto.BanksoftDeliveryDate;

            viewModel.ExpectedStatus =
                dto.ExpectedStatus;

            viewModel.CurrentStatus =
                dto.CurrentStatus;

            viewModel.AnalystNote =
                dto.AnalystNote;
        }
    }
}