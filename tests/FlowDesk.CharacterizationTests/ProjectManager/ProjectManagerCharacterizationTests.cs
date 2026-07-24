using System.Net;
using FlowDesk.CharacterizationTests.Infrastructure;
using FlowDesk.Constants;
using FlowDesk.Data;
using FlowDesk.Models;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.CharacterizationTests.ProjectManager;

public sealed class ProjectManagerCharacterizationTests
    : DatabaseTestBase
{
    [Fact]
    public async Task Create_ValidRequest_CreatesSubmittedWorkItem()
    {
        using HttpClient client = CreateClient().AuthenticateAs(
            101,
            AppRoles.ProjectManager,
            TestDataSeeder.UniqueEmail("pm"));
        string requestNumber = TestDataSeeder.UniqueRequestNumber("CREATE");

        HttpResponseMessage response =
            await client.PostFormWithAntiforgeryAsync(
                "/ProjectManager/Create",
                "/ProjectManager/Create",
                ValidWorkItemForm(requestNumber));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        WorkItem? workItem = await WithServicesAsync(async services =>
            await services.GetRequiredService<AppDbContext>()
                .WorkItems.AsNoTracking()
                .SingleOrDefaultAsync(x => x.RequestNumber == requestNumber));

        Assert.NotNull(workItem);
        Assert.Equal("Characterization request", workItem.RequestDescription);
        Assert.Equal(TestDataSeeder.DefaultDepartment, workItem.Department);
        Assert.Equal(RequestPriority.High, workItem.Priority);
        Assert.Equal(WorkflowStatus.Submitted, workItem.WorkflowStatus);
    }

    [Fact]
    public async Task Create_AuthenticatedProjectManager_AssignsCurrentUserId()
    {
        const int userId = 741;
        using HttpClient client = CreateClient().AuthenticateAs(
            userId,
            AppRoles.ProjectManager,
            TestDataSeeder.UniqueEmail("pm-owner"));
        string requestNumber = TestDataSeeder.UniqueRequestNumber("OWNER");

        await client.PostFormWithAntiforgeryAsync(
            "/ProjectManager/Create",
            "/ProjectManager/Create",
            ValidWorkItemForm(requestNumber));

        int? createdByUserId = await WithServicesAsync(async services =>
            await services.GetRequiredService<AppDbContext>()
                .WorkItems.AsNoTracking()
                .Where(x => x.RequestNumber == requestNumber)
                .Select(x => x.CreatedByUserId)
                .SingleAsync());

        Assert.Equal(userId, createdByUserId);
    }

    [Fact]
    public async Task Edit_ValidSubmittedRequest_UpdatesEditableFields()
    {
        const int userId = 202;
        WorkItem seeded = await WithServicesAsync(services =>
            TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.Submitted,
                userId));
        using HttpClient client = CreateClient().AuthenticateAs(
            userId,
            AppRoles.ProjectManager,
            TestDataSeeder.UniqueEmail("pm-edit"));
        string updatedNumber = TestDataSeeder.UniqueRequestNumber("EDITED");

        HttpResponseMessage response =
            await client.PostFormWithAntiforgeryAsync(
                $"/ProjectManager/Edit/{seeded.Id}",
                $"/ProjectManager/Edit/{seeded.Id}",
                new Dictionary<string, string>
                {
                    ["Id"] = seeded.Id.ToString(),
                    ["RequestNumber"] = updatedNumber,
                    ["RequestDescription"] = "Updated description",
                    ["Department"] = TestDataSeeder.DefaultDepartment,
                    ["Priority"] = ((int)RequestPriority.Critical).ToString()
                });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        WorkItem updated = await WithServicesAsync(async services =>
            await services.GetRequiredService<AppDbContext>()
                .WorkItems.AsNoTracking()
                .SingleAsync(x => x.Id == seeded.Id));

        Assert.Equal(updatedNumber, updated.RequestNumber);
        Assert.Equal("Updated description", updated.RequestDescription);
        Assert.Equal(RequestPriority.Critical, updated.Priority);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public async Task Delete_SubmittedRequest_RemovesWorkItem()
    {
        const int userId = 303;
        WorkItem seeded = await WithServicesAsync(services =>
            TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.Submitted,
                userId));
        using HttpClient client = CreateClient().AuthenticateAs(
            userId,
            AppRoles.ProjectManager,
            TestDataSeeder.UniqueEmail("pm-delete"));

        HttpResponseMessage response =
            await client.PostFormWithAntiforgeryAsync(
                $"/ProjectManager/Delete/{seeded.Id}",
                $"/ProjectManager/Delete/{seeded.Id}",
                new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        bool exists = await WithServicesAsync(async services =>
            await services.GetRequiredService<AppDbContext>()
                .WorkItems.AnyAsync(x => x.Id == seeded.Id));

        Assert.False(exists);
    }

    [Fact]
    public async Task Create_InvalidModel_DoesNotCreateWorkItem()
    {
        using HttpClient client = CreateClient().AuthenticateAs(
            404,
            AppRoles.ProjectManager,
            TestDataSeeder.UniqueEmail("pm-invalid"));

        Dictionary<string, string> invalidForm =
            ValidWorkItemForm(TestDataSeeder.UniqueRequestNumber("invalid"));
        invalidForm["RequestDescription"] = string.Empty;

        HttpResponseMessage response =
            await client.PostFormWithAntiforgeryAsync(
                "/ProjectManager/Create",
                "/ProjectManager/Create",
                invalidForm);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        int count = await WithServicesAsync(async services =>
            await services.GetRequiredService<AppDbContext>()
                .WorkItems.CountAsync());

        Assert.Equal(0, count);
    }

    private static Dictionary<string, string> ValidWorkItemForm(
        string requestNumber)
    {
        return new Dictionary<string, string>
        {
            ["RequestNumber"] = requestNumber,
            ["RequestDescription"] = "Characterization request",
            ["Department"] = TestDataSeeder.DefaultDepartment,
            ["Priority"] = ((int)RequestPriority.High).ToString()
        };
    }
}