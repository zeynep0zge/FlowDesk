using System.Security.Claims;
using FlowDesk.CharacterizationTests.Infrastructure;
using FlowDesk.Common;
using FlowDesk.Constants;
using FlowDesk.Data;
using FlowDesk.Models;
using FlowDesk.Services.Interfaces;
using FlowDesk.Services.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace FlowDesk.CharacterizationTests.WorkItemQueries;

public sealed class AuthorizedWorkItemQueryServiceTests
    : DatabaseTestBase
{
    private static string OtherDepartment => DepartmentOptions.All
        .First(value => value != TestDataSeeder.DefaultDepartment);

    public AuthorizedWorkItemQueryServiceTests()
        : base(Environments.Development)
    {
    }

    [Fact]
    public async Task ProjectManager_CanReadOnlyOwnWorkItem()
    {
        ApplicationUser projectManager = await CreateUserAsync(
            "query-pm-own",
            AppRoles.ProjectManager);
        WorkItem own = await CreateWorkItemAsync(
            WorkflowStatus.Submitted,
            createdByUserId: projectManager.Id);
        ActorContext actor = await ResolveActorAsync(projectManager.Id);

        ServiceResult<WorkItemDetailsResult> result =
            await GetDetailsAsync(own.Id, actor);

        Assert.True(result.IsSuccess);
        Assert.Equal(own.Id, result.Data!.Id);
    }

    [Fact]
    public async Task ProjectManager_CannotReadAnotherOwnersWorkItem()
    {
        ApplicationUser projectManager = await CreateUserAsync(
            "query-pm-other",
            AppRoles.ProjectManager);
        WorkItem other = await CreateWorkItemAsync(
            WorkflowStatus.Submitted,
            createdByUserId: projectManager.Id + 1);
        ActorContext actor = await ResolveActorAsync(projectManager.Id);

        ServiceResult<WorkItemDetailsResult> result =
            await GetDetailsAsync(other.Id, actor);

        Assert.True(result.IsNotFound);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task Analyst_CanReadUnassignedSubmittedWorkItem()
    {
        ApplicationUser analyst = await CreateUserAsync(
            "query-analyst-submitted",
            AppRoles.Analyst);
        WorkItem submitted = await CreateWorkItemAsync(
            WorkflowStatus.Submitted);
        ActorContext actor = await ResolveActorAsync(analyst.Id);

        ServiceResult<WorkItemDetailsResult> result =
            await GetDetailsAsync(submitted.Id, actor);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Analyst_CanReadOwnAssignedWorkItem()
    {
        ApplicationUser analyst = await CreateUserAsync(
            "query-analyst-own",
            AppRoles.Analyst);
        WorkItem own = await CreateWorkItemAsync(
            WorkflowStatus.UnderAnalystReview,
            analystId: analyst.Id);
        ActorContext actor = await ResolveActorAsync(analyst.Id);

        ServiceResult<WorkItemDetailsResult> result =
            await GetDetailsAsync(own.Id, actor);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Analyst_CannotReadAnotherAnalystsWorkItem()
    {
        ApplicationUser analyst = await CreateUserAsync(
            "query-analyst-denied",
            AppRoles.Analyst);
        ApplicationUser otherAnalyst = await CreateUserAsync(
            "query-other-analyst",
            AppRoles.Analyst);
        WorkItem other = await CreateWorkItemAsync(
            WorkflowStatus.UnderAnalystReview,
            analystId: otherAnalyst.Id);
        WorkItem claimedSubmitted = await CreateWorkItemAsync(
            WorkflowStatus.Submitted,
            analystId: otherAnalyst.Id);
        ActorContext actor = await ResolveActorAsync(analyst.Id);

        ServiceResult<PagedResult<WorkItemSummaryResult>> result =
            await SearchAsync(actor);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(result.Data!.Items, item =>
            item.Id == other.Id || item.Id == claimedSubmitted.Id);
    }

    [Fact]
    public async Task NormalManager_SeesOnlyAllowedStatusesInOwnDepartment()
    {
        WorkItem waiting = await CreateWorkItemAsync(
            WorkflowStatus.WaitingManagerApproval);
        WorkItem approved = await CreateWorkItemAsync(
            WorkflowStatus.Approved);
        WorkItem wrongStatus = await CreateWorkItemAsync(
            WorkflowStatus.UnderAnalystReview);
        WorkItem otherDepartment = await CreateWorkItemAsync(
            WorkflowStatus.WaitingManagerApproval,
            department: OtherDepartment);
        ActorContext actor = await ResolveActorAsync(9001);

        ServiceResult<PagedResult<WorkItemSummaryResult>> result =
            await SearchAsync(actor);

        int[] ids = result.Data!.Items.Select(item => item.Id).ToArray();
        Assert.Contains(waiting.Id, ids);
        Assert.Contains(approved.Id, ids);
        Assert.DoesNotContain(wrongStatus.Id, ids);
        Assert.DoesNotContain(otherDepartment.Id, ids);
    }

    [Fact]
    public async Task DevelopmentGlobalManager_CanReadOtherDepartment()
    {
        ApplicationUser manager = await CreateUserAsync(
            "query-global-manager",
            AppRoles.DepartmentManager,
            DepartmentOptions.AllDepartments);
        WorkItem otherDepartment = await CreateWorkItemAsync(
            WorkflowStatus.Approved,
            department: OtherDepartment);
        ActorContext actor = await ResolveActorAsync(manager.Id);

        ServiceResult<WorkItemDetailsResult> result =
            await GetDetailsAsync(otherDepartment.Id, actor);

        Assert.True(actor.CanAccessAllDepartments);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ProductionGlobalManager_FailsClosedDuringResolution()
    {
        using var factory = new FlowDeskWebApplicationFactory(
            Environments.Production);
        await factory.ResetDatabaseAsync();

        ApplicationUser manager;
        await using (AsyncServiceScope scope =
                     factory.Services.CreateAsyncScope())
        {
            manager = await TestDataSeeder.CreateUserAsync(
                scope.ServiceProvider,
                TestDataSeeder.UniqueEmail("query-production-global"),
                assignedRole: AppRoles.DepartmentManager,
                department: DepartmentOptions.AllDepartments);
        }

        await using AsyncServiceScope resolveScope =
            factory.Services.CreateAsyncScope();
        var result = await resolveScope.ServiceProvider
            .GetRequiredService<IAuthenticatedActorContextResolver>()
            .ResolveAsync(CreatePrincipal(manager.Id));

        Assert.True(result.IsForbidden);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task Employee_IsForbiddenForDetailsAndSearch()
    {
        WorkItem workItem = await CreateWorkItemAsync(
            WorkflowStatus.Approved,
            developerId: 22);
        ActorContext actor = await ResolveActorAsync(22);

        ServiceResult<WorkItemDetailsResult> details =
            await GetDetailsAsync(workItem.Id, actor);
        ServiceResult<PagedResult<WorkItemSummaryResult>> search =
            await SearchAsync(actor);

        Assert.True(details.IsForbidden);
        Assert.True(search.IsForbidden);
        Assert.Null(details.Data);
        Assert.Null(search.Data);
    }

    [Fact]
    public async Task RolelessActor_FailsClosedDuringResolution()
    {
        ApplicationUser user = await CreateUserAsync(
            "query-roleless",
            assignedRole: null);

        ServiceResult<ActorContext> result = await WithServicesAsync(
            services => services
                .GetRequiredService<IAuthenticatedActorContextResolver>()
                .ResolveAsync(CreatePrincipal(user.Id)));

        Assert.True(result.IsForbidden);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task DeletedActor_FailsClosedDuringResolution()
    {
        ApplicationUser user = await CreateUserAsync(
            "query-deleted",
            AppRoles.ProjectManager);
        await WithServicesAsync(async services =>
        {
            UserManager<ApplicationUser> userManager =
                services.GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser current = (await userManager.FindByIdAsync(
                user.Id.ToString()))!;
            IdentityResult deleteResult =
                await userManager.DeleteAsync(current);
            Assert.True(deleteResult.Succeeded);
        });

        ServiceResult<ActorContext> result = await WithServicesAsync(
            services => services
                .GetRequiredService<IAuthenticatedActorContextResolver>()
                .ResolveAsync(CreatePrincipal(user.Id)));

        Assert.True(result.IsForbidden);
    }

    [Fact]
    public async Task Search_PageSizeIsCappedAt50AndPageAtLeast1()
    {
        ApplicationUser projectManager = await CreateUserAsync(
            "query-page",
            AppRoles.ProjectManager);
        await CreateManyWorkItemsAsync(projectManager.Id, 55);
        ActorContext actor = await ResolveActorAsync(projectManager.Id);

        ServiceResult<PagedResult<WorkItemSummaryResult>> result =
            await SearchAsync(
                actor,
                new AuthorizedWorkItemSearchQuery
                {
                    Page = 0,
                    PageSize = 500
                });

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data!.Page);
        Assert.Equal(50, result.Data.PageSize);
        Assert.Equal(50, result.Data.Items.Count);
        Assert.Equal(55, result.Data.TotalCount);
    }

    [Fact]
    public async Task SearchFilter_CannotExpandProjectManagerScope()
    {
        ApplicationUser projectManager = await CreateUserAsync(
            "query-filter-scope",
            AppRoles.ProjectManager);
        WorkItem own = await CreateWorkItemAsync(
            WorkflowStatus.Submitted,
            createdByUserId: projectManager.Id);
        WorkItem other = await CreateWorkItemAsync(
            WorkflowStatus.Submitted,
            createdByUserId: projectManager.Id + 1);
        ActorContext actor = await ResolveActorAsync(projectManager.Id);

        ServiceResult<PagedResult<WorkItemSummaryResult>> result =
            await SearchAsync(
                actor,
                new AuthorizedWorkItemSearchQuery
                {
                    SearchText = "Characterization test request",
                    Department = TestDataSeeder.DefaultDepartment,
                    WorkflowStatus = WorkflowStatus.Submitted
                });

        Assert.Contains(result.Data!.Items, item => item.Id == own.Id);
        Assert.DoesNotContain(result.Data.Items, item => item.Id == other.Id);
    }

    [Fact]
    public void ResultContracts_DoNotExposeEntityOrRazorTypes()
    {
        Type[] resultTypes =
        [
            typeof(WorkItemSummaryResult),
            typeof(WorkItemDetailsResult)
        ];

        Assert.All(resultTypes, type => Assert.DoesNotContain(
            type.GetProperties(),
            property =>
                typeof(WorkItem).IsAssignableFrom(property.PropertyType) ||
                property.PropertyType.Namespace?.Contains(
                    "ViewModels",
                    StringComparison.Ordinal) == true));

        string[] summaryProperties = typeof(WorkItemSummaryResult)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();
        Assert.DoesNotContain(nameof(WorkItem.AnalystNote), summaryProperties);
        Assert.DoesNotContain(nameof(WorkItem.ManagerNote), summaryProperties);
        Assert.DoesNotContain(nameof(WorkItem.CurrentStatus), summaryProperties);
    }

    private Task<ApplicationUser> CreateUserAsync(
        string prefix,
        string? assignedRole,
        string? department = null)
    {
        return WithServicesAsync(services =>
            TestDataSeeder.CreateUserAsync(
                services,
                TestDataSeeder.UniqueEmail(prefix),
                assignedRole: assignedRole,
                department: department));
    }

    private Task<WorkItem> CreateWorkItemAsync(
        WorkflowStatus status,
        int? createdByUserId = 101,
        string? department = null,
        int? analystId = null,
        int? developerId = null)
    {
        return WithServicesAsync(services =>
            TestDataSeeder.CreateWorkItemAsync(
                services,
                status,
                createdByUserId,
                department,
                analystId,
                developerId));
    }

    private async Task CreateManyWorkItemsAsync(int ownerId, int count)
    {
        await WithServicesAsync(async services =>
        {
            AppDbContext context =
                services.GetRequiredService<AppDbContext>();
            context.WorkItems.AddRange(Enumerable.Range(1, count).Select(
                index => new WorkItem
                {
                    RequestNumber =
                        TestDataSeeder.UniqueRequestNumber($"PAGE-{index}"),
                    RequestDescription = "Paged query item",
                    Department = TestDataSeeder.DefaultDepartment,
                    Priority = RequestPriority.Normal,
                    CreatedByUserId = ownerId,
                    WorkflowStatus = WorkflowStatus.Submitted,
                    CurrentStatus = WorkflowStatus.Submitted.ToString(),
                    CreatedAt = DateTime.UtcNow.AddMinutes(-index)
                }));
            await context.SaveChangesAsync();
        });
    }

    private Task<ActorContext> ResolveActorAsync(int userId)
    {
        return WithServicesAsync(async services =>
        {
            ServiceResult<ActorContext> result = await services
                .GetRequiredService<IAuthenticatedActorContextResolver>()
                .ResolveAsync(CreatePrincipal(userId));
            Assert.True(result.IsSuccess);
            return result.Data!;
        });
    }

    private Task<ServiceResult<WorkItemDetailsResult>> GetDetailsAsync(
        int workItemId,
        ActorContext actor)
    {
        return WithServicesAsync(services => services
            .GetRequiredService<IAuthorizedWorkItemQueryService>()
            .GetDetailsAsync(workItemId, actor));
    }

    private Task<ServiceResult<PagedResult<WorkItemSummaryResult>>>
        SearchAsync(
            ActorContext actor,
            AuthorizedWorkItemSearchQuery? query = null)
    {
        return WithServicesAsync(services => services
            .GetRequiredService<IAuthorizedWorkItemQueryService>()
            .SearchAsync(
                query ?? new AuthorizedWorkItemSearchQuery(),
                actor));
    }

    private static ClaimsPrincipal CreatePrincipal(int userId)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, AppRoles.Employee)
        ],
        "QueryTests"));
    }
}
