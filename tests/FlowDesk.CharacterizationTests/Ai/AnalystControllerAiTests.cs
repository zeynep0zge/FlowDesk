using System.Reflection;
using System.Security.Claims;
using FlowDesk.Ai.Interfaces;
using FlowDesk.Ai.Models;
using FlowDesk.Common;
using FlowDesk.Constants;
using FlowDesk.Controllers;
using FlowDesk.DTOs.Analyst;
using FlowDesk.Services.Interfaces;
using FlowDesk.Services.Models;
using FlowDesk.ViewModels.Analyst;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.CharacterizationTests.Ai;

public sealed class AnalystControllerAiTests
{
    [Fact]
    public async Task RewriteWithAi_Success_ReturnsAiResult()
    {
        ActorContext actor = CreateAnalyst();
        var resolver = new StubActorContextResolver
        {
            Result = ServiceResult<ActorContext>.Success(actor)
        };
        AnalystAiDraftResult expected = CreateDraftResult();
        var workflow = new StubAnalystAiWorkflowService
        {
            RewriteResult = ServiceResult<AnalystAiDraftResult>.Success(
                expected)
        };
        AnalystController controller = CreateController(resolver, workflow);

        IActionResult actionResult = await controller.RewriteWithAi(
            42,
            CancellationToken.None);

        OkObjectResult okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Same(expected, okResult.Value);
        Assert.Equal(42, workflow.WorkItemId);
        Assert.Same(actor, workflow.Actor);
    }

    [Fact]
    public async Task RewriteWithAi_ActorForbidden_DoesNotCallWorkflow()
    {
        var resolver = new StubActorContextResolver
        {
            Result = ServiceResult<ActorContext>.Forbidden("Yetkisiz.")
        };
        var workflow = new StubAnalystAiWorkflowService();
        AnalystController controller = CreateController(resolver, workflow);

        IActionResult actionResult = await controller.RewriteWithAi(
            42,
            CancellationToken.None);

        Assert.IsType<ForbidResult>(actionResult);
        Assert.Equal(0, workflow.CallCount);
    }

    [Fact]
    public async Task RewriteWithAi_ActorFailure_ReturnsErrorMessage()
    {
        const string errorMessage = "Kullanıcı bilgisi çözümlenemedi.";
        var resolver = new StubActorContextResolver
        {
            Result = ServiceResult<ActorContext>.Failure(errorMessage)
        };
        var workflow = new StubAnalystAiWorkflowService();
        AnalystController controller = CreateController(resolver, workflow);

        IActionResult actionResult = await controller.RewriteWithAi(
            42,
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult);
        Assert.Equal(errorMessage, GetErrorMessage(badRequest));
        Assert.Equal(0, workflow.CallCount);
    }

    [Fact]
    public async Task RewriteWithAi_WorkflowNotFound_ReturnsNotFound()
    {
        var workflow = new StubAnalystAiWorkflowService
        {
            RewriteResult = ServiceResult<AnalystAiDraftResult>.NotFound(
                "Talep bulunamadı.")
        };
        AnalystController controller = CreateController(
            CreateSuccessfulResolver(),
            workflow);

        IActionResult actionResult = await controller.RewriteWithAi(
            42,
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(actionResult);
    }

    [Fact]
    public async Task RewriteWithAi_WorkflowForbidden_ReturnsForbid()
    {
        var workflow = new StubAnalystAiWorkflowService
        {
            RewriteResult = ServiceResult<AnalystAiDraftResult>.Forbidden(
                "Bu talebe erişim yetkiniz bulunmuyor.")
        };
        AnalystController controller = CreateController(
            CreateSuccessfulResolver(),
            workflow);

        IActionResult actionResult = await controller.RewriteWithAi(
            42,
            CancellationToken.None);

        Assert.IsType<ForbidResult>(actionResult);
    }

    [Fact]
    public async Task RewriteWithAi_WorkflowFailure_PreservesErrorMessage()
    {
        const string errorMessage = "AI servisi isteği tamamlayamadı.";
        var workflow = new StubAnalystAiWorkflowService
        {
            RewriteResult = ServiceResult<AnalystAiDraftResult>.Failure(
                errorMessage)
        };
        AnalystController controller = CreateController(
            CreateSuccessfulResolver(),
            workflow);

        IActionResult actionResult = await controller.RewriteWithAi(
            42,
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult);
        Assert.Equal(errorMessage, GetErrorMessage(badRequest));
    }

    [Fact]
    public async Task RewriteWithAi_ForwardsCancellationToken()
    {
        var workflow = new StubAnalystAiWorkflowService();
        AnalystController controller = CreateController(
            CreateSuccessfulResolver(),
            workflow);
        using var cancellationTokenSource = new CancellationTokenSource();

        await controller.RewriteWithAi(
            42,
            cancellationTokenSource.Token);

        Assert.Equal(
            cancellationTokenSource.Token,
            workflow.CancellationToken);
    }

    [Fact]
    public async Task SaveAiDraft_Success_ReturnsUpdatedDraft()
    {
        AnalystAiDraftResult expected = CreateDraftResult();
        var workflow = new StubAnalystAiWorkflowService
        {
            SaveResult = ServiceResult<AnalystAiDraftResult>.Success(expected)
        };
        AnalystController controller = CreateController(
            CreateSuccessfulResolver(),
            workflow);
        var request = new SaveAnalystAiDraftRequest
        {
            EditedRequest = "Kaydedilecek metin",
            RowVersion = "AQID"
        };

        IActionResult actionResult = await controller.SaveAiDraft(
            42,
            request,
            CancellationToken.None);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Same(expected, ok.Value);
        Assert.Same(request, workflow.SaveRequest);
    }

    [Fact]
    public async Task SaveAiDraft_ValidationFailure_ReturnsBadRequest()
    {
        var workflow = new StubAnalystAiWorkflowService
        {
            SaveResult = ServiceResult<AnalystAiDraftResult>.Failure(
                "Düzenlenmiş AI taslağı boş olamaz.")
        };
        AnalystController controller = CreateController(
            CreateSuccessfulResolver(),
            workflow);

        IActionResult actionResult = await controller.SaveAiDraft(
            42,
            new SaveAnalystAiDraftRequest(),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult);
        Assert.Equal(
            "Düzenlenmiş AI taslağı boş olamaz.",
            GetErrorMessage(badRequest));
    }

    [Fact]
    public async Task SaveAiDraft_ConcurrencyFailure_ReturnsConflict()
    {
        const string message =
            "AI taslağı başka bir işlem tarafından güncellendi.";
        var workflow = new StubAnalystAiWorkflowService
        {
            SaveResult = ServiceResult<AnalystAiDraftResult>.Conflict(message)
        };
        AnalystController controller = CreateController(
            CreateSuccessfulResolver(),
            workflow);

        IActionResult actionResult = await controller.SaveAiDraft(
            42,
            new SaveAnalystAiDraftRequest(),
            CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(actionResult);
        Assert.Equal(
            message,
            conflict.Value?.GetType().GetProperty("errorMessage")?
                .GetValue(conflict.Value));
    }

    [Fact]
    public async Task SaveAiDraft_ForwardsCancellationToken()
    {
        var workflow = new StubAnalystAiWorkflowService();
        AnalystController controller = CreateController(
            CreateSuccessfulResolver(),
            workflow);
        using var cancellationTokenSource = new CancellationTokenSource();

        await controller.SaveAiDraft(
            42,
            new SaveAnalystAiDraftRequest(),
            cancellationTokenSource.Token);

        Assert.Equal(
            cancellationTokenSource.Token,
            workflow.SaveCancellationToken);
    }

    private static AnalystController CreateController(
        StubActorContextResolver resolver,
        StubAnalystAiWorkflowService workflow)
    {
        var controller = new AnalystController(
            new UnusedAnalystWorkflowService(),
            resolver,
            workflow)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return controller;
    }

    private static StubActorContextResolver CreateSuccessfulResolver()
    {
        return new StubActorContextResolver
        {
            Result = ServiceResult<ActorContext>.Success(CreateAnalyst())
        };
    }

    private static ActorContext CreateAnalyst()
    {
        return (ActorContext)Activator.CreateInstance(
            typeof(ActorContext),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: [101, AppRoles.Analyst, "Bilgi Teknolojileri", false],
            culture: null)!;
    }

    private static string? GetErrorMessage(
        BadRequestObjectResult badRequest)
    {
        return badRequest.Value?
            .GetType()
            .GetProperty("errorMessage")?
            .GetValue(badRequest.Value) as string;
    }

    private static AnalystAiDraftResult CreateDraftResult()
    {
        return new AnalystAiDraftResult
        {
            RewrittenRequest = "Düzenlenmiş talep",
            RowVersion = "AQID",
            GeneratedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private sealed class StubActorContextResolver
        : IAuthenticatedActorContextResolver
    {
        public ServiceResult<ActorContext> Result { get; init; }
            = ServiceResult<ActorContext>.Failure("Ayarlanmadı.");

        public Task<ServiceResult<ActorContext>> ResolveAsync(
            ClaimsPrincipal principal)
        {
            return Task.FromResult(Result);
        }
    }

    private sealed class StubAnalystAiWorkflowService
        : IAnalystAiWorkflowService
    {
        public ServiceResult<AnalystAiDraftResult> RewriteResult { get; init; }
            = ServiceResult<AnalystAiDraftResult>.Failure(
                "Ayarlanmadı.");

        public ServiceResult<AnalystAiDraftResult> SaveResult { get; init; }
            = ServiceResult<AnalystAiDraftResult>.Failure("Ayarlanmadı.");

        public int CallCount { get; private set; }

        public int WorkItemId { get; private set; }

        public ActorContext? Actor { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public SaveAnalystAiDraftRequest? SaveRequest { get; private set; }

        public CancellationToken SaveCancellationToken { get; private set; }

        public Task<ServiceResult<AnalystAiDraftResult>>
            RewriteWorkItemAsync(
                int workItemId,
                ActorContext actor,
                CancellationToken cancellationToken = default)
        {
            CallCount++;
            WorkItemId = workItemId;
            Actor = actor;
            CancellationToken = cancellationToken;
            return Task.FromResult(RewriteResult);
        }

        public Task<ServiceResult<AnalystAiDraftResult>> SaveEditedDraftAsync(
            int workItemId,
            ActorContext actor,
            SaveAnalystAiDraftRequest request,
            CancellationToken cancellationToken = default)
        {
            WorkItemId = workItemId;
            Actor = actor;
            SaveRequest = request;
            SaveCancellationToken = cancellationToken;
            return Task.FromResult(SaveResult);
        }
    }

    private sealed class UnusedAnalystWorkflowService
        : IAnalystWorkflowService
    {
        public Task<ServiceResult<AnalystInboxViewModel>> GetInboxAsync(
            int? currentAnalystId)
        {
            throw new NotSupportedException();
        }

        public Task<ServiceResult<List<AnalystInboxItemViewModel>>>
            GetReturnedRequestsAsync(int? currentAnalystId)
        {
            throw new NotSupportedException();
        }

        public Task<ServiceResult<AnalystReviewViewModel>> GetReviewAsync(
            int id,
            int? currentAnalystId)
        {
            throw new NotSupportedException();
        }

        public Task<ServiceResult> StartReviewAsync(
            int id,
            int? currentAnalystId)
        {
            throw new NotSupportedException();
        }

        public Task<ServiceResult<AnalystReviewViewModel>> SaveAnalysisAsync(
            SaveAnalysisDto dto,
            int? currentAnalystId)
        {
            throw new NotSupportedException();
        }

        public Task<ServiceResult> SubmitForApprovalAsync(
            SubmitForApprovalDto dto,
            int? currentAnalystId)
        {
            throw new NotSupportedException();
        }
    }
}
