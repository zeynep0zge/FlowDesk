using System.Net;
using FlowDesk.CharacterizationTests.Infrastructure;
using FlowDesk.Constants;
using FlowDesk.Data;
using FlowDesk.Models;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.CharacterizationTests.Authorization;

public sealed class ProjectManagerAuthorizationTests : DatabaseTestBase
{
    [Theory]
    [InlineData("Details")]
    [InlineData("Edit")]
    [InlineData("Delete")]
    public async Task NullOwnerWorkItem_GetActions_AreForbidden(string action)
    {
        WorkItem target = await CreateWorkItemAsync(null);
        using HttpClient client = ProjectManagerClient(101);

        HttpResponseMessage response = await client.GetAsync(
            $"/ProjectManager/{action}/{target.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task NullOwnerWorkItem_EditPost_DoesNotChangeIt()
    {
        WorkItem target = await CreateWorkItemAsync(null);
        WorkItem own = await CreateWorkItemAsync(101);
        using HttpClient client = ProjectManagerClient(101);

        HttpResponseMessage response =
            await client.PostFormWithAntiforgeryAsync(
                $"/ProjectManager/Edit/{own.Id}",
                $"/ProjectManager/Edit/{target.Id}",
                new Dictionary<string, string>
                {
                    ["Id"] = target.Id.ToString(),
                    ["RequestNumber"] = target.RequestNumber,
                    ["RequestDescription"] = "Unauthorized change",
                    ["Department"] = TestDataSeeder.DefaultDepartment,
                    ["Priority"] = ((int)RequestPriority.High).ToString()
                });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(
            "Unauthorized change",
            (await GetWorkItemAsync(target.Id)).RequestDescription);
    }

    [Fact]
    public async Task NullOwnerWorkItem_DeletePost_DoesNotDeleteIt()
    {
        WorkItem target = await CreateWorkItemAsync(null);
        WorkItem own = await CreateWorkItemAsync(101);
        using HttpClient client = ProjectManagerClient(101);

        HttpResponseMessage response =
            await client.PostFormWithAntiforgeryAsync(
                $"/ProjectManager/Delete/{own.Id}",
                $"/ProjectManager/Delete/{target.Id}",
                new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotNull(await GetWorkItemAsync(target.Id));
    }

    [Fact]
    public async Task InvalidUserIdClaim_IndexAndCreatePost_AreForbidden()
    {
        using HttpClient client = CreateClient();
        client.DefaultRequestHeaders.Add(
            TestAuthenticationHandler.UserIdHeader,
            "0");
        client.DefaultRequestHeaders.Add(
            TestAuthenticationHandler.RoleHeader,
            AppRoles.ProjectManager);
        client.DefaultRequestHeaders.Add(
            TestAuthenticationHandler.EmailHeader,
            TestDataSeeder.UniqueEmail("invalid-claim"));

        HttpResponseMessage indexResponse =
            await client.GetAsync("/ProjectManager");
        HttpResponseMessage createResponse =
            await client.PostFormWithAntiforgeryAsync(
                "/ProjectManager/Create",
                "/ProjectManager/Create",
                new Dictionary<string, string>
                {
                    ["RequestNumber"] =
                        TestDataSeeder.UniqueRequestNumber("INVALID-CLAIM"),
                    ["RequestDescription"] = "Must not be created",
                    ["Department"] = TestDataSeeder.DefaultDepartment,
                    ["Priority"] = ((int)RequestPriority.Normal).ToString()
                });

        Assert.NotEqual(HttpStatusCode.OK, indexResponse.StatusCode);
        Assert.NotEqual(HttpStatusCode.Redirect, createResponse.StatusCode);
        int count = await WithServicesAsync(async services =>
            await services.GetRequiredService<AppDbContext>()
                .WorkItems.CountAsync());
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task MissingUserIdClaim_IndexDoesNotReturnList()
    {
        using HttpClient client = CreateClient();

        HttpResponseMessage response =
            await client.GetAsync("/ProjectManager");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    private HttpClient ProjectManagerClient(int userId)
    {
        return CreateClient().AuthenticateAs(
            userId,
            AppRoles.ProjectManager,
            TestDataSeeder.UniqueEmail($"pm-{userId}"));
    }

    private Task<WorkItem> CreateWorkItemAsync(int? ownerId)
    {
        return WithServicesAsync(services =>
            TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.Submitted,
                ownerId));
    }

    private Task<WorkItem> GetWorkItemAsync(int id)
    {
        return WithServicesAsync(async services =>
            await services.GetRequiredService<AppDbContext>()
                .WorkItems.AsNoTracking()
                .SingleAsync(x => x.Id == id));
    }
}
