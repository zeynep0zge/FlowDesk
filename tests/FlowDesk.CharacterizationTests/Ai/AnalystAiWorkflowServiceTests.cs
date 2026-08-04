using System.Reflection;
using FlowDesk.Ai.Entities;
using FlowDesk.Ai.Interfaces;
using FlowDesk.Ai.Models;
using FlowDesk.Ai.Options;
using FlowDesk.Ai.Repositories.Interfaces;
using FlowDesk.Ai.Services;
using FlowDesk.Common;
using FlowDesk.Constants;
using FlowDesk.Models;
using FlowDesk.Services.Interfaces;
using FlowDesk.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FlowDesk.CharacterizationTests.Ai;

public sealed class AnalystAiWorkflowServiceTests
{
    [Fact]
    public async Task RewriteWorkItemAsync_ExistingDraft_DoesNotCallGemini()
    {
        var repository = new StubDraftRepository
        {
            NoTrackingDraft = CreateDraft()
        };
        var rewriteService = new StubAnalystRequestRewriteService();
        var researchService = new StubUnresolvedTermResearchService();
        var service = CreateService(
            repository,
            rewriteService: rewriteService,
            researchService: researchService);

        ServiceResult<AnalystAiDraftResult> result =
            await service.RewriteWorkItemAsync(42, CreateAnalyst());

        Assert.True(result.IsSuccess);
        Assert.Equal(0, rewriteService.CallCount);
        Assert.Equal(0, researchService.CallCount);
        Assert.Equal(1, repository.NoTrackingReadCallCount);
    }

    [Fact]
    public async Task RewriteWorkItemAsync_NoUnresolvedTerms_DoesNotResearch()
    {
        var repository = new StubDraftRepository();
        var rewriteService = new StubAnalystRequestRewriteService
        {
            Result = ServiceResult<AnalystRequestRewriteResult>.Success(
                CreateRewriteResult("Gemini düzenlemesi", []))
        };
        var researchService = new StubUnresolvedTermResearchService();
        var service = CreateService(
            repository,
            rewriteService: rewriteService,
            researchService: researchService);

        ServiceResult<AnalystAiDraftResult> result =
            await service.RewriteWorkItemAsync(42, CreateAnalyst());

        Assert.True(result.IsSuccess);
        Assert.Equal(0, researchService.CallCount);
    }

    [Fact]
    public async Task RewriteWorkItemAsync_ResolvedTerm_MovesToAbbreviations()
    {
        var repository = new StubDraftRepository();
        var rewriteService = new StubAnalystRequestRewriteService
        {
            Result = ServiceResult<AnalystRequestRewriteResult>.Success(
                CreateRewriteResult("Gemini düzenlemesi", ["BKM"]))
        };
        var researchService = new StubUnresolvedTermResearchService
        {
            Result = ServiceResult<UnresolvedTermResearchResult>.Success(
                new UnresolvedTermResearchResult
                {
                    Terms =
                    [
                        new ResearchedTerm
                        {
                            Term = "BKM",
                            IsResolved = true,
                            ExpandedForm = "Bankalararası Kart Merkezi",
                            Explanation = "Türkiye'deki kartlı ödeme kuruluşu."
                        }
                    ]
                })
        };
        var service = CreateService(
            repository,
            rewriteService: rewriteService,
            researchService: researchService);

        ServiceResult<AnalystAiDraftResult> result =
            await service.RewriteWorkItemAsync(42, CreateAnalyst());

        Assert.True(result.IsSuccess);
        Assert.Contains(
            result.Data!.Abbreviations,
            item => item.Abbreviation == "BKM" &&
                item.ExpandedForm == "Bankalararası Kart Merkezi");
        Assert.DoesNotContain("BKM", result.Data.UnresolvedTerms);
        Assert.Equal("BKM", Assert.Single(researchService.Terms!));
    }

    [Fact]
    public async Task RewriteWorkItemAsync_UnresolvedResearchTerm_RemainsUnresolved()
    {
        var researchService = new StubUnresolvedTermResearchService
        {
            Result = ServiceResult<UnresolvedTermResearchResult>.Success(
                new UnresolvedTermResearchResult
                {
                    Terms =
                    [
                        new ResearchedTerm
                        {
                            Term = "Kurum içi terim",
                            IsResolved = false
                        }
                    ]
                })
        };
        var service = CreateService(
            new StubDraftRepository(),
            rewriteService: new StubAnalystRequestRewriteService
            {
                Result = ServiceResult<AnalystRequestRewriteResult>.Success(
                    CreateRewriteResult("Gemini düzenlemesi"))
            },
            researchService: researchService);

        ServiceResult<AnalystAiDraftResult> result =
            await service.RewriteWorkItemAsync(42, CreateAnalyst());

        Assert.True(result.IsSuccess);
        Assert.Contains("Kurum içi terim", result.Data!.UnresolvedTerms);
    }

    [Fact]
    public async Task RewriteWorkItemAsync_ResearchFailure_SavesRewriteDraft()
    {
        var repository = new StubDraftRepository();
        var researchService = new StubUnresolvedTermResearchService
        {
            Result = ServiceResult<UnresolvedTermResearchResult>.Failure(
                "Araştırma başarısız.")
        };
        var service = CreateService(
            repository,
            rewriteService: new StubAnalystRequestRewriteService
            {
                Result = ServiceResult<AnalystRequestRewriteResult>.Success(
                    CreateRewriteResult("Kaydedilecek taslak"))
            },
            researchService: researchService);

        ServiceResult<AnalystAiDraftResult> result =
            await service.RewriteWorkItemAsync(42, CreateAnalyst());

        Assert.True(result.IsSuccess);
        Assert.Equal("Kaydedilecek taslak", repository.AddedDraft?.GeneratedRequest);
        Assert.Contains("Kurum içi terim", result.Data!.UnresolvedTerms);
        Assert.Equal(1, repository.SaveCallCount);
    }

    [Fact]
    public async Task RewriteWorkItemAsync_ExistingDraft_ReturnsEditedRequest()
    {
        var repository = new StubDraftRepository
        {
            NoTrackingDraft = CreateDraft(
                generatedRequest: "İlk Gemini çıktısı",
                editedRequest: "Analistin son düzenlemesi")
        };
        var service = CreateService(repository);

        ServiceResult<AnalystAiDraftResult> result =
            await service.RewriteWorkItemAsync(42, CreateAnalyst());

        Assert.True(result.IsSuccess);
        Assert.Equal("Analistin son düzenlemesi", result.Data?.RewrittenRequest);
    }

    [Fact]
    public async Task RewriteWorkItemAsync_MissingDraft_CallsGeminiAndCreatesDraft()
    {
        var repository = new StubDraftRepository();
        var rewriteService = new StubAnalystRequestRewriteService
        {
            Result = ServiceResult<AnalystRequestRewriteResult>.Success(
                CreateRewriteResult("Gemini düzenlemesi"))
        };
        var service = CreateService(repository, rewriteService: rewriteService);

        ServiceResult<AnalystAiDraftResult> result =
            await service.RewriteWorkItemAsync(42, CreateAnalyst());

        Assert.True(result.IsSuccess);
        Assert.Equal(1, rewriteService.CallCount);
        Assert.Equal(1, repository.AddCallCount);
        Assert.Equal(1, repository.SaveCallCount);
        Assert.Equal("Orijinal talep", rewriteService.Description);
        Assert.Equal("Gemini düzenlemesi", repository.AddedDraft?.GeneratedRequest);
        Assert.Equal("Gemini düzenlemesi", repository.AddedDraft?.EditedRequest);
        Assert.Equal("gemini-test-model", repository.AddedDraft?.ModelName);
    }

    [Fact]
    public async Task RewriteWorkItemAsync_GeminiFailure_DoesNotCreateDraft()
    {
        var repository = new StubDraftRepository();
        var rewriteService = new StubAnalystRequestRewriteService
        {
            Result = ServiceResult<AnalystRequestRewriteResult>.Failure(
                "Gemini başarısız.")
        };
        var service = CreateService(repository, rewriteService: rewriteService);

        ServiceResult<AnalystAiDraftResult> result =
            await service.RewriteWorkItemAsync(42, CreateAnalyst());

        Assert.False(result.IsSuccess);
        Assert.Equal("Gemini başarısız.", result.ErrorMessage);
        Assert.Equal(0, repository.AddCallCount);
        Assert.Equal(0, repository.SaveCallCount);
    }

    [Fact]
    public async Task RewriteWorkItemAsync_UnauthorizedWorkItem_DoesNotReadDraftOrCallGemini()
    {
        var queryService = new StubAuthorizedWorkItemQueryService
        {
            DetailsResult = ServiceResult<WorkItemDetailsResult>.Forbidden(
                "Bu talebe erişim yetkiniz bulunmuyor.")
        };
        var repository = new StubDraftRepository();
        var rewriteService = new StubAnalystRequestRewriteService();
        var service = CreateService(
            repository,
            queryService,
            rewriteService);

        ServiceResult<AnalystAiDraftResult> result =
            await service.RewriteWorkItemAsync(42, CreateAnalyst());

        Assert.True(result.IsForbidden);
        Assert.Equal(0, repository.NoTrackingReadCallCount);
        Assert.Equal(0, rewriteService.CallCount);
    }

    [Fact]
    public async Task RewriteWorkItemAsync_NonAnalyst_ReturnsForbidden()
    {
        var queryService = new StubAuthorizedWorkItemQueryService();
        var repository = new StubDraftRepository();
        var service = CreateService(repository, queryService);

        ServiceResult<AnalystAiDraftResult> result =
            await service.RewriteWorkItemAsync(
                42,
                CreateActor(AppRoles.ProjectManager));

        Assert.True(result.IsForbidden);
        Assert.Equal(0, queryService.GetDetailsCallCount);
        Assert.Equal(0, repository.NoTrackingReadCallCount);
    }

    [Fact]
    public async Task RewriteWorkItemAsync_CorruptJson_ReturnsControlledFailure()
    {
        WorkItemAiDraft draft = CreateDraft();
        draft.AmbiguitiesJson = "not-json";
        var service = CreateService(new StubDraftRepository
        {
            NoTrackingDraft = draft
        });

        ServiceResult<AnalystAiDraftResult> result =
            await service.RewriteWorkItemAsync(42, CreateAnalyst());

        Assert.False(result.IsSuccess);
        Assert.Equal("Kayıtlı AI taslağı okunamadı.", result.ErrorMessage);
    }

    [Fact]
    public async Task RewriteWorkItemAsync_UniqueConflict_ReturnsConcurrentDraft()
    {
        WorkItemAiDraft concurrentDraft = CreateDraft(
            editedRequest: "Diğer isteğin kaydettiği taslak");
        var repository = new StubDraftRepository
        {
            NoTrackingDraftSequence = new Queue<WorkItemAiDraft?>(
                [null, concurrentDraft]),
            SaveException = new DbUpdateException("duplicate")
        };
        var rewriteService = new StubAnalystRequestRewriteService
        {
            Result = ServiceResult<AnalystRequestRewriteResult>.Success(
                CreateRewriteResult("Bu isteğin Gemini çıktısı"))
        };
        var service = CreateService(repository, rewriteService: rewriteService);

        ServiceResult<AnalystAiDraftResult> result =
            await service.RewriteWorkItemAsync(42, CreateAnalyst());

        Assert.True(result.IsSuccess);
        Assert.Equal(
            "Diğer isteğin kaydettiği taslak",
            result.Data?.RewrittenRequest);
        Assert.Equal(2, repository.NoTrackingReadCallCount);
    }

    [Fact]
    public async Task SaveEditedDraftAsync_ValidRequest_SavesEditedRequest()
    {
        WorkItemAiDraft draft = CreateDraft(
            generatedRequest: "Korunacak Gemini çıktısı",
            editedRequest: "Önceki düzenleme");
        var repository = new StubDraftRepository { TrackedDraft = draft };
        var service = CreateService(repository);

        ServiceResult<AnalystAiDraftResult> result =
            await service.SaveEditedDraftAsync(
                42,
                CreateAnalyst(),
                new SaveAnalystAiDraftRequest
                {
                    EditedRequest = "  Yeni analist düzenlemesi  ",
                    RowVersion = Convert.ToBase64String([1, 2, 3])
                });

        Assert.True(result.IsSuccess);
        Assert.Equal("Yeni analist düzenlemesi", draft.EditedRequest);
        Assert.Equal(1, repository.SaveCallCount);
        Assert.Equal<byte>([1, 2, 3], repository.OriginalRowVersion!);
    }

    [Fact]
    public async Task SaveEditedDraftAsync_DoesNotChangeGeneratedRequestOrLists()
    {
        WorkItemAiDraft draft = CreateDraft(
            generatedRequest: "Korunacak Gemini çıktısı");
        string abbreviationsJson = draft.AbbreviationsJson;
        var repository = new StubDraftRepository { TrackedDraft = draft };
        var service = CreateService(repository);

        await service.SaveEditedDraftAsync(
            42,
            CreateAnalyst(),
            CreateSaveRequest("Yeni metin"));

        Assert.Equal("Korunacak Gemini çıktısı", draft.GeneratedRequest);
        Assert.Equal(abbreviationsJson, draft.AbbreviationsJson);
    }

    [Fact]
    public async Task SaveEditedDraftAsync_EmptyRequest_ReturnsFailure()
    {
        var repository = new StubDraftRepository();
        var service = CreateService(repository);

        ServiceResult<AnalystAiDraftResult> result =
            await service.SaveEditedDraftAsync(
                42,
                CreateAnalyst(),
                CreateSaveRequest("   "));

        Assert.False(result.IsSuccess);
        Assert.Equal(0, repository.TrackedReadCallCount);
    }

    [Fact]
    public async Task SaveEditedDraftAsync_LongRequest_ReturnsFailure()
    {
        var repository = new StubDraftRepository();
        var service = CreateService(repository);

        ServiceResult<AnalystAiDraftResult> result =
            await service.SaveEditedDraftAsync(
                42,
                CreateAnalyst(),
                CreateSaveRequest(new string(
                    'x',
                    WorkItemAiDraft.RequestMaximumLength + 1)));

        Assert.False(result.IsSuccess);
        Assert.Equal(0, repository.TrackedReadCallCount);
    }

    [Fact]
    public async Task SaveEditedDraftAsync_InvalidRowVersion_ReturnsFailure()
    {
        var repository = new StubDraftRepository();
        var service = CreateService(repository);

        ServiceResult<AnalystAiDraftResult> result =
            await service.SaveEditedDraftAsync(
                42,
                CreateAnalyst(),
                new SaveAnalystAiDraftRequest
                {
                    EditedRequest = "Yeni metin",
                    RowVersion = "not-base64"
                });

        Assert.False(result.IsSuccess);
        Assert.Equal("AI taslağı sürüm bilgisi geçersiz.", result.ErrorMessage);
        Assert.Equal(0, repository.TrackedReadCallCount);
    }

    [Fact]
    public async Task SaveEditedDraftAsync_MissingDraft_ReturnsNotFound()
    {
        var service = CreateService(new StubDraftRepository());

        ServiceResult<AnalystAiDraftResult> result =
            await service.SaveEditedDraftAsync(
                42,
                CreateAnalyst(),
                CreateSaveRequest("Yeni metin"));

        Assert.True(result.IsNotFound);
    }

    [Fact]
    public async Task SaveEditedDraftAsync_UnauthorizedActor_DoesNotReadDraft()
    {
        var repository = new StubDraftRepository();
        var service = CreateService(repository);

        ServiceResult<AnalystAiDraftResult> result =
            await service.SaveEditedDraftAsync(
                42,
                CreateActor(AppRoles.ProjectManager),
                CreateSaveRequest("Yeni metin"));

        Assert.True(result.IsForbidden);
        Assert.Equal(0, repository.TrackedReadCallCount);
    }

    [Fact]
    public async Task SaveEditedDraftAsync_ConcurrencyConflict_ReturnsConflict()
    {
        var repository = new StubDraftRepository
        {
            TrackedDraft = CreateDraft(),
            SaveException = new DbUpdateConcurrencyException()
        };
        var service = CreateService(repository);

        ServiceResult<AnalystAiDraftResult> result =
            await service.SaveEditedDraftAsync(
                42,
                CreateAnalyst(),
                CreateSaveRequest("Yeni metin"));

        Assert.True(result.IsConflict);
        Assert.Equal(
            "AI taslağı başka bir işlem tarafından güncellendi. " +
            "Sayfayı yenileyip tekrar deneyin.",
            result.ErrorMessage);
    }

    private static AnalystAiWorkflowService CreateService(
        StubDraftRepository repository,
        StubAuthorizedWorkItemQueryService? queryService = null,
        StubAnalystRequestRewriteService? rewriteService = null,
        StubUnresolvedTermResearchService? researchService = null)
    {
        return new AnalystAiWorkflowService(
            queryService ?? new StubAuthorizedWorkItemQueryService
            {
                DetailsResult = ServiceResult<WorkItemDetailsResult>.Success(
                    CreateWorkItem("Orijinal talep"))
            },
            rewriteService ?? new StubAnalystRequestRewriteService(),
            researchService ?? new StubUnresolvedTermResearchService(),
            repository,
            Microsoft.Extensions.Options.Options.Create(new GeminiOptions
            {
                ApiKey = "test-key",
                Model = "gemini-test-model"
            }));
    }

    private static AnalystRequestRewriteResult CreateRewriteResult(
        string rewrittenRequest,
        IReadOnlyList<string>? unresolvedTerms = null)
    {
        return new AnalystRequestRewriteResult
        {
            RewrittenRequest = rewrittenRequest,
            Abbreviations =
            [
                new AbbreviationExplanation
                {
                    Abbreviation = "ATM",
                    ExpandedForm = "Automated Teller Machine"
                }
            ],
            Ambiguities = ["Belirsizlik"],
            UnresolvedTerms = unresolvedTerms ?? ["Kurum içi terim"]
        };
    }

    private static WorkItemAiDraft CreateDraft(
        string generatedRequest = "Gemini çıktısı",
        string editedRequest = "Analist düzenlemesi")
    {
        return new WorkItemAiDraft
        {
            Id = 7,
            WorkItemId = 42,
            GeneratedRequest = generatedRequest,
            EditedRequest = editedRequest,
            AbbreviationsJson = "[]",
            AmbiguitiesJson = "[]",
            UnresolvedTermsJson = "[]",
            ModelName = "gemini-test-model",
            GeneratedAt = DateTime.UtcNow.AddMinutes(-1),
            UpdatedAt = DateTime.UtcNow,
            RowVersion = [1, 2, 3]
        };
    }

    private static SaveAnalystAiDraftRequest CreateSaveRequest(string text)
    {
        return new SaveAnalystAiDraftRequest
        {
            EditedRequest = text,
            RowVersion = Convert.ToBase64String([1, 2, 3])
        };
    }

    private static ActorContext CreateAnalyst() =>
        CreateActor(AppRoles.Analyst);

    private static ActorContext CreateActor(string role)
    {
        return (ActorContext)Activator.CreateInstance(
            typeof(ActorContext),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: [101, role, "Bilgi Teknolojileri", false],
            culture: null)!;
    }

    private static WorkItemDetailsResult CreateWorkItem(string description)
    {
        return new WorkItemDetailsResult(
            42,
            "TLP-42",
            description,
            "Bilgi Teknolojileri",
            RequestPriority.Normal,
            WorkflowStatus.Submitted,
            DateTime.UtcNow,
            null,
            null,
            null,
            null,
            null,
            null,
            null);
    }

    private sealed class StubAuthorizedWorkItemQueryService
        : IAuthorizedWorkItemQueryService
    {
        public ServiceResult<WorkItemDetailsResult> DetailsResult { get; init; }
            = ServiceResult<WorkItemDetailsResult>.Failure("Ayarlanmadı.");

        public int GetDetailsCallCount { get; private set; }

        public Task<ServiceResult<WorkItemDetailsResult>> GetDetailsAsync(
            int workItemId,
            ActorContext actor)
        {
            GetDetailsCallCount++;
            return Task.FromResult(DetailsResult);
        }

        public Task<ServiceResult<PagedResult<WorkItemSummaryResult>>>
            SearchAsync(
                AuthorizedWorkItemSearchQuery query,
                ActorContext actor) => throw new NotSupportedException();
    }

    private sealed class StubAnalystRequestRewriteService
        : IAnalystRequestRewriteService
    {
        public ServiceResult<AnalystRequestRewriteResult> Result { get; init; }
            = ServiceResult<AnalystRequestRewriteResult>.Failure("Ayarlanmadı.");

        public int CallCount { get; private set; }

        public string? Description { get; private set; }

        public Task<ServiceResult<AnalystRequestRewriteResult>> RewriteAsync(
            string originalRequestDescription,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Description = originalRequestDescription;
            return Task.FromResult(Result);
        }
    }

    private sealed class StubUnresolvedTermResearchService
        : IUnresolvedTermResearchService
    {
        public ServiceResult<UnresolvedTermResearchResult> Result { get; init; }
            = ServiceResult<UnresolvedTermResearchResult>.Failure(
                "Ayarlanmadı.");

        public int CallCount { get; private set; }

        public IReadOnlyCollection<string>? Terms { get; private set; }

        public Task<ServiceResult<UnresolvedTermResearchResult>> ResearchAsync(
            IReadOnlyCollection<string> unresolvedTerms,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Terms = unresolvedTerms;
            return Task.FromResult(Result);
        }
    }

    private sealed class StubDraftRepository : IWorkItemAiDraftRepository
    {
        public WorkItemAiDraft? NoTrackingDraft { get; init; }

        public Queue<WorkItemAiDraft?>? NoTrackingDraftSequence { get; init; }

        public WorkItemAiDraft? TrackedDraft { get; init; }

        public Exception? SaveException { get; init; }

        public int NoTrackingReadCallCount { get; private set; }

        public int TrackedReadCallCount { get; private set; }

        public int AddCallCount { get; private set; }

        public int SaveCallCount { get; private set; }

        public WorkItemAiDraft? AddedDraft { get; private set; }

        public byte[]? OriginalRowVersion { get; private set; }

        public Task<WorkItemAiDraft?> GetByWorkItemIdAsNoTrackingAsync(
            int workItemId,
            CancellationToken cancellationToken = default)
        {
            NoTrackingReadCallCount++;
            WorkItemAiDraft? result = NoTrackingDraftSequence?.Count > 0
                ? NoTrackingDraftSequence.Dequeue()
                : NoTrackingDraft;
            return Task.FromResult(result);
        }

        public Task<WorkItemAiDraft?> GetByWorkItemIdAsync(
            int workItemId,
            CancellationToken cancellationToken = default)
        {
            TrackedReadCallCount++;
            return Task.FromResult(TrackedDraft);
        }

        public void Add(WorkItemAiDraft draft)
        {
            AddCallCount++;
            AddedDraft = draft;
        }

        public void SetOriginalRowVersion(
            WorkItemAiDraft draft,
            byte[] rowVersion)
        {
            OriginalRowVersion = rowVersion;
        }

        public Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            SaveCallCount++;
            if (SaveException != null)
            {
                throw SaveException;
            }

            if (AddedDraft != null && AddedDraft.RowVersion.Length == 0)
            {
                AddedDraft.RowVersion = [9, 9, 9];
            }

            return Task.FromResult(1);
        }
    }
}
