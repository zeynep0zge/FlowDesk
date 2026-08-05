using FlowDesk.Ai.Interfaces;
using FlowDesk.Ai.Models;
using FlowDesk.Constants;
using FlowDesk.DTOs.Analyst;
using FlowDesk.Services.Interfaces;
using FlowDesk.ViewModels.Analyst;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FlowDesk.Controllers
{
    [Authorize(Roles = AppRoles.Analyst)]
    public class AnalystController : Controller
    {
        private readonly IAnalystWorkflowService
            _analystWorkflowService;
        private readonly IAuthenticatedActorContextResolver
            _authenticatedActorContextResolver;
        private readonly IAnalystAiWorkflowService
            _analystAiWorkflowService;

        public AnalystController(
            IAnalystWorkflowService analystWorkflowService,
            IAuthenticatedActorContextResolver
                authenticatedActorContextResolver,
            IAnalystAiWorkflowService analystAiWorkflowService)
        {
            _analystWorkflowService = analystWorkflowService;
            _authenticatedActorContextResolver =
                authenticatedActorContextResolver;
            _analystAiWorkflowService = analystAiWorkflowService;
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
                await _analystWorkflowService.GetInboxAsync(
                    GetCurrentUserId());

            if (result.IsForbidden)
            {
                return Forbid();
            }

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
                await _analystWorkflowService.GetReviewAsync(
                    id,
                    GetCurrentUserId());

            if (result.IsNotFound)
            {
                return NotFound();
            }

            if (result.IsForbidden)
            {
                return Forbid();
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
                    .StartReviewAsync(id, GetCurrentUserId());

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
        public async Task<IActionResult> RewriteWithAi(
            int id,
            CancellationToken cancellationToken)
        {
            var actorResult =
                await _authenticatedActorContextResolver.ResolveAsync(User);

            if (actorResult.IsForbidden)
            {
                return Forbid();
            }

            if (!actorResult.IsSuccess || actorResult.Data == null)
            {
                return BadRequest(new
                {
                    errorMessage = actorResult.ErrorMessage ??
                        "Kullanıcı bilgileri doğrulanamadı."
                });
            }

            var result = await _analystAiWorkflowService
                .RewriteWorkItemAsync(
                    id,
                    actorResult.Data,
                    cancellationToken);

            if (result.IsNotFound)
            {
                return NotFound();
            }

            if (result.IsForbidden)
            {
                return Forbid();
            }

            if (!result.IsSuccess || result.Data == null)
            {
                return BadRequest(new
                {
                    errorMessage = result.ErrorMessage ??
                        "AI düzenleme işlemi tamamlanamadı."
                });
            }

            return Ok(result.Data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAiDraft(
            int id,
            SaveAnalystAiDraftRequest request,
            CancellationToken cancellationToken)
        {
            var actorResult =
                await _authenticatedActorContextResolver.ResolveAsync(User);

            if (actorResult.IsForbidden)
            {
                return Forbid();
            }

            if (!actorResult.IsSuccess || actorResult.Data == null)
            {
                return BadRequest(new
                {
                    errorMessage = actorResult.ErrorMessage ??
                        "Kullanıcı bilgileri doğrulanamadı."
                });
            }

            var result = await _analystAiWorkflowService
                .SaveEditedDraftAsync(
                    id,
                    actorResult.Data,
                    request,
                    cancellationToken);

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
                return Conflict(new
                {
                    errorMessage = result.ErrorMessage
                });
            }

            if (!result.IsSuccess || result.Data == null)
            {
                return BadRequest(new
                {
                    errorMessage = result.ErrorMessage ??
                        "AI taslağı kaydedilemedi."
                });
            }

            return Ok(result.Data);
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
                    .SaveAnalysisAsync(
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
                ModelState.AddModelError(
                    string.Empty,
                    result.ErrorMessage ??
                    "Analiz bilgileri kaydedilemedi."
                );

                var reviewResult =
                    await _analystWorkflowService
                        .GetReviewAsync(
                            id,
                            GetCurrentUserId());

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
            int id,
            SubmitForApprovalDto dto)
        {
            dto.WorkItemId = id;

            var result =
                await _analystWorkflowService
                    .SubmitForApprovalAsync(
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
                ModelState.AddModelError(
                    string.Empty,
                    result.ErrorMessage ??
                    "Talep yönetici onayına gönderilemedi."
                );

                var reviewResult =
                    await _analystWorkflowService
                        .GetReviewAsync(
                            id,
                            GetCurrentUserId());

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
                "Talep departman yöneticisinin " +
                "onayına gönderildi.";

            return RedirectToAction(nameof(Inbox));
        }

        [HttpGet]
        public async Task<IActionResult> ReturnedRequests()
        {
            var result =
                await _analystWorkflowService
                    .GetReturnedRequestsAsync(
                        GetCurrentUserId());

            if (result.IsForbidden)
            {
                return Forbid();
            }

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

        private int? GetCurrentUserId()
        {
            string? userIdValue = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            return int.TryParse(userIdValue, out int userId) && userId > 0
                ? userId
                : null;
        }

        private static void ApplyPostedValues(
            AnalystReviewViewModel viewModel,
            SaveAnalysisDto dto)
        {
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
