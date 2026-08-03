using System.Net;
using FlowDesk.CharacterizationTests.Infrastructure;
using FlowDesk.Common;
using FlowDesk.Constants;
using FlowDesk.Data;
using FlowDesk.DTOs.Analyst;
using FlowDesk.Models;
using FlowDesk.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.CharacterizationTests.Authorization;

public sealed class AnalystAuthorizationTests : DatabaseTestBase
{
    [Fact]
    public async Task StartReview_AssignsAuthenticatedAnalystAndBlocksSecondAnalyst()
    {
        WorkItem target = await WithServicesAsync(services =>
            TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.Submitted));

        var results = await WithServicesAsync(async services =>
        {
            IAnalystWorkflowService service =
                services.GetRequiredService<IAnalystWorkflowService>();
            var first = await service.StartReviewAsync(target.Id, 11);
            var second = await service.StartReviewAsync(target.Id, 12);
            return (first, second);
        });

        Assert.True(results.first.IsSuccess);
        Assert.True(results.second.IsForbidden);
        Assert.Equal(11, (await GetWorkItemAsync(target.Id)).AnalystId);
    }

    [Fact]
    public async Task Review_OtherAnalystWorkItem_IsForbidden()
    {
        WorkItem target = await CreateOwnedWorkItemAsync(11);
        using HttpClient client = AnalystClient(12);

        HttpResponseMessage response =
            await client.GetAsync($"/Analyst/Review/{target.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SaveAnalysis_OtherAnalystWorkItem_DoesNotChangeIt()
    {
        WorkItem target = await CreateOwnedWorkItemAsync(11);
        WorkItem own = await CreateOwnedWorkItemAsync(12);
        using HttpClient client = AnalystClient(12);

        HttpResponseMessage response =
            await client.PostFormWithAntiforgeryAsync(
                $"/Analyst/Review/{own.Id}",
                $"/Analyst/SaveAnalysis/{target.Id}",
                ValidAnalysisForm(999));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        WorkItem persisted = await GetWorkItemAsync(target.Id);
        Assert.Null(persisted.DeveloperId);
        Assert.Equal(11, persisted.AnalystId);
    }

    [Fact]
    public async Task SubmitForApproval_OtherAnalystWorkItem_DoesNotChangeIt()
    {
        WorkItem target = await CreateOwnedWorkItemAsync(11);
        WorkItem own = await CreateOwnedWorkItemAsync(12);
        using HttpClient client = AnalystClient(12);

        HttpResponseMessage response =
            await client.PostFormWithAntiforgeryAsync(
                $"/Analyst/Review/{own.Id}",
                $"/Analyst/SubmitForApproval/{target.Id}",
                ValidAnalysisForm(999));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(
            WorkflowStatus.UnderAnalystReview,
            (await GetWorkItemAsync(target.Id)).WorkflowStatus);
    }

    [Fact]
    public async Task SaveAnalysis_PostedAnalystId_IsIgnored()
    {
        WorkItem target = await CreateOwnedWorkItemAsync(11);
        using HttpClient client = AnalystClient(11);

        HttpResponseMessage response =
            await client.PostFormWithAntiforgeryAsync(
                $"/Analyst/Review/{target.Id}",
                $"/Analyst/SaveAnalysis/{target.Id}",
                ValidAnalysisForm(999));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(11, (await GetWorkItemAsync(target.Id)).AnalystId);
    }

    [Theory]
    [InlineData(987654)]
    [InlineData(9001)]
    public async Task SaveAnalysis_InvalidOrNonEmployeeDeveloper_IsRejected(
        int developerId)
    {
        await WithServicesAsync(async services =>
        {
            WorkItem target = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.UnderAnalystReview,
                analystId: 11);
            IAnalystWorkflowService service =
                services.GetRequiredService<IAnalystWorkflowService>();

            var result = await service.SaveAnalysisAsync(
                ValidAnalysisDto(target.Id, developerId),
                11);

            Assert.False(result.IsSuccess);
            Assert.Null(target.DeveloperId);
        });
    }

    [Fact]
    public async Task SaveAnalysis_OtherDepartmentEmployee_IsRejected()
    {
        const int developerId = 5000;
        await WithServicesAsync(async services =>
        {
            await TestDataSeeder.CreateUserAsync(
                services,
                TestDataSeeder.UniqueEmail("other-department-employee"),
                assignedRole: AppRoles.Employee,
                department: DepartmentOptions.All.First(
                    x => x != TestDataSeeder.DefaultDepartment),
                userId: developerId);
            WorkItem target = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.UnderAnalystReview,
                analystId: 11);
            IAnalystWorkflowService service =
                services.GetRequiredService<IAnalystWorkflowService>();

            var result = await service.SaveAnalysisAsync(
                ValidAnalysisDto(target.Id, developerId),
                11);

            Assert.False(result.IsSuccess);
            Assert.Null(target.DeveloperId);
        });
    }

    [Fact]
    public async Task ReturnedRequests_OnlyContainsCurrentAnalystItems()
    {
        WorkItem own = await WithServicesAsync(services =>
            TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.ReturnedToAnalyst,
                analystId: 11));
        WorkItem other = await WithServicesAsync(services =>
            TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.ReturnedToAnalyst,
                analystId: 12));

        var result = await WithServicesAsync(async services =>
            await services.GetRequiredService<IAnalystWorkflowService>()
                .GetReturnedRequestsAsync(11));

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Data!, x => x.Id == own.Id);
        Assert.DoesNotContain(result.Data!, x => x.Id == other.Id);
    }

    private HttpClient AnalystClient(int userId)
    {
        return CreateClient().AuthenticateAs(
            userId,
            AppRoles.Analyst,
            TestDataSeeder.UniqueEmail($"analyst-{userId}"));
    }

    private Task<WorkItem> CreateOwnedWorkItemAsync(int analystId)
    {
        return WithServicesAsync(services =>
            TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.UnderAnalystReview,
                analystId: analystId));
    }

    private Task<WorkItem> GetWorkItemAsync(int id)
    {
        return WithServicesAsync(async services =>
            await services.GetRequiredService<AppDbContext>()
                .WorkItems.AsNoTracking()
                .SingleAsync(x => x.Id == id));
    }

    private static Dictionary<string, string> ValidAnalysisForm(
        int analystId)
    {
        return new Dictionary<string, string>
        {
            ["AnalystId"] = analystId.ToString(),
            ["DeveloperId"] = "22",
            ["ReleaseDate"] = "2026-09-20",
            ["BanksoftDeliveryDate"] = "2026-09-10",
            ["ExpectedStatus"] = "Ready",
            ["CurrentStatus"] = AnalystCurrentStatusOptions.UnderAnalystReview,
            ["AnalystNote"] = "Authorization test"
        };
    }

    private static SaveAnalysisDto ValidAnalysisDto(
        int workItemId,
        int developerId)
    {
        return new SaveAnalysisDto
        {
            WorkItemId = workItemId,
            AnalystId = 999,
            DeveloperId = developerId,
            ReleaseDate = new DateTime(2026, 9, 20),
            BanksoftDeliveryDate = new DateTime(2026, 9, 10),
            ExpectedStatus = "Ready",
            CurrentStatus = AnalystCurrentStatusOptions.UnderAnalystReview,
            AnalystNote = "Authorization test"
        };
    }
}
