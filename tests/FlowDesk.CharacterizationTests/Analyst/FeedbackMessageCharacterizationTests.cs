using FlowDesk.CharacterizationTests.Infrastructure;
using FlowDesk.Constants;
using FlowDesk.Data;
using FlowDesk.Models;
using FlowDesk.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace FlowDesk.CharacterizationTests.Analyst;

public sealed class FeedbackMessageCharacterizationTests
    : DatabaseTestBase
{
    [Fact]
    public async Task AnalystAndOwner_ExchangeOrderedMessages_WithoutStatusChange()
    {
        await WithServicesAsync(async services =>
        {
            await CreateParticipantAsync(
                services,
                11,
                AppRoles.Analyst,
                "Yetkili Analist");
            await CreateParticipantAsync(
                services,
                101,
                AppRoles.ProjectManager,
                "İş Birimi Sahibi");
            WorkItem workItem = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.UnderAnalystReview,
                createdByUserId: 101,
                analystId: 11);

            IAnalystWorkflowService analystService = services
                .GetRequiredService<IAnalystWorkflowService>();
            IProjectManagerWorkItemService ownerService = services
                .GetRequiredService<IProjectManagerWorkItemService>();

            Assert.True((await analystService.SendReviewFeedbackAsync(
                workItem.Id,
                "Eksik alanı açıklar mısınız?",
                11)).IsSuccess);
            Assert.True((await ownerService.SendFeedbackMessageAsync(
                workItem.Id,
                "Alanın beklenen değeri aktiftir.",
                101)).IsSuccess);

            var analystReview = await analystService.GetReviewAsync(
                workItem.Id,
                11);
            var ownerDetails = await ownerService.GetDetailsAsync(
                workItem.Id,
                101);

            Assert.True(analystReview.IsSuccess);
            Assert.True(ownerDetails.IsSuccess);
            Assert.Equal(WorkflowStatus.UnderAnalystReview,
                workItem.WorkflowStatus);
            Assert.Equal(
                new[]
                {
                    "Eksik alanı açıklar mısınız?",
                    "Alanın beklenen değeri aktiftir."
                },
                analystReview.Data!.FeedbackMessages
                    .Select(message => message.Message));
            Assert.Equal(new[] { 11, 101 },
                ownerDetails.Data!.FeedbackMessages
                    .Select(message => message.SenderUserId));
            Assert.All(ownerDetails.Data.FeedbackMessages,
                message => Assert.NotEqual(default, message.CreatedAt));
            List<FeedbackMessage> persistedMessages = await services
                .GetRequiredService<AppDbContext>()
                .FeedbackMessages.AsNoTracking()
                .OrderBy(message => message.Id)
                .ToListAsync();
            Assert.All(persistedMessages,
                message => Assert.True(message.IsRead));
            Assert.All(persistedMessages,
                message => Assert.NotNull(message.ReadAt));
        });
    }

    [Fact]
    public async Task Navbar_ShowsOnlyUnreadCounterpartMessages_UntilOpened()
    {
        WorkItem workItem = await WithServicesAsync(async services =>
        {
            await CreateParticipantAsync(
                services,
                11,
                AppRoles.Analyst,
                "Yetkili Analist");
            await CreateParticipantAsync(
                services,
                101,
                AppRoles.ProjectManager,
                "İş Birimi Sahibi");
            WorkItem item = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.UnderAnalystReview,
                createdByUserId: 101,
                analystId: 11);
            Assert.True((await services
                .GetRequiredService<IAnalystWorkflowService>()
                .SendReviewFeedbackAsync(
                    item.Id,
                    "Navbar analist mesajı",
                    11)).IsSuccess);
            return item;
        });

        using HttpClient ownerClient = CreateClient().AuthenticateAs(
            101,
            AppRoles.ProjectManager,
            TestDataSeeder.UniqueEmail("notification-owner"));
        string ownerInbox = WebUtility.HtmlDecode(
            await ownerClient.GetStringAsync("/ProjectManager/Index"));
        Assert.Contains("Navbar analist mesajı", ownerInbox);

        await ownerClient.GetStringAsync(
            $"/ProjectManager/Details/{workItem.Id}");
        string ownerInboxAfterRead = WebUtility.HtmlDecode(
            await ownerClient.GetStringAsync("/ProjectManager/Index"));
        Assert.DoesNotContain(
            "Navbar analist mesajı",
            ownerInboxAfterRead);

        await WithServicesAsync(async services =>
        {
            Assert.True((await services
                .GetRequiredService<IProjectManagerWorkItemService>()
                .SendFeedbackMessageAsync(
                    workItem.Id,
                    "Navbar İş Birimi cevabı",
                    101)).IsSuccess);
        });

        using HttpClient analystClient = CreateClient().AuthenticateAs(
            11,
            AppRoles.Analyst,
            TestDataSeeder.UniqueEmail("notification-analyst"));
        string analystInbox = WebUtility.HtmlDecode(
            await analystClient.GetStringAsync("/Analyst/Inbox"));
        Assert.Contains("Navbar İş Birimi cevabı", analystInbox);

        await analystClient.GetStringAsync(
            $"/Analyst/Review/{workItem.Id}");
        string analystInboxAfterRead = WebUtility.HtmlDecode(
            await analystClient.GetStringAsync("/Analyst/Inbox"));
        Assert.DoesNotContain(
            "Navbar İş Birimi cevabı",
            analystInboxAfterRead);
    }

    [Fact]
    public async Task MessagePosts_RedirectToRoleHomePages()
    {
        WorkItem workItem = await WithServicesAsync(async services =>
        {
            await CreateParticipantAsync(
                services,
                11,
                AppRoles.Analyst,
                "Yetkili Analist");
            await CreateParticipantAsync(
                services,
                101,
                AppRoles.ProjectManager,
                "İş Birimi Sahibi");
            return await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.UnderAnalystReview,
                createdByUserId: 101,
                analystId: 11);
        });

        using HttpClient analystClient = CreateClient().AuthenticateAs(
            11,
            AppRoles.Analyst,
            TestDataSeeder.UniqueEmail("redirect-analyst"));
        HttpResponseMessage analystResponse = await analystClient
            .PostFormWithAntiforgeryAsync(
                $"/Analyst/Review/{workItem.Id}",
                $"/Analyst/SendReviewFeedback/{workItem.Id}",
                new Dictionary<string, string>
                {
                    ["message"] = "Analist redirect mesajı"
                });

        Assert.Equal("/Analyst/Inbox",
            analystResponse.Headers.Location?.OriginalString);

        using HttpClient ownerClient = CreateClient().AuthenticateAs(
            101,
            AppRoles.ProjectManager,
            TestDataSeeder.UniqueEmail("redirect-owner"));
        HttpResponseMessage ownerResponse = await ownerClient
            .PostFormWithAntiforgeryAsync(
                $"/ProjectManager/Details/{workItem.Id}",
                $"/ProjectManager/SendFeedbackMessage/{workItem.Id}",
                new Dictionary<string, string>
                {
                    ["message"] = "İş Birimi redirect mesajı"
                });

        Assert.Equal("/ProjectManager/Index",
            ownerResponse.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task NonParticipants_CannotReadOrSendMessages()
    {
        await WithServicesAsync(async services =>
        {
            await CreateParticipantAsync(
                services,
                11,
                AppRoles.Analyst,
                "Yetkili Analist");
            await CreateParticipantAsync(
                services,
                12,
                AppRoles.Analyst,
                "Diğer Analist");
            await CreateParticipantAsync(
                services,
                101,
                AppRoles.ProjectManager,
                "İş Birimi Sahibi");
            await CreateParticipantAsync(
                services,
                102,
                AppRoles.ProjectManager,
                "Diğer İş Birimi");
            WorkItem workItem = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.UnderAnalystReview,
                createdByUserId: 101,
                analystId: 11);

            IAnalystWorkflowService analystService = services
                .GetRequiredService<IAnalystWorkflowService>();
            IProjectManagerWorkItemService ownerService = services
                .GetRequiredService<IProjectManagerWorkItemService>();

            Assert.True((await analystService.SendReviewFeedbackAsync(
                workItem.Id,
                "Yetkisiz mesaj",
                12)).IsForbidden);
            Assert.True((await ownerService.SendFeedbackMessageAsync(
                workItem.Id,
                "Yetkisiz cevap",
                102)).IsForbidden);
            Assert.True((await analystService.GetReviewAsync(
                workItem.Id,
                12)).IsForbidden);
            Assert.True((await ownerService.GetDetailsAsync(
                workItem.Id,
                102)).IsForbidden);
            Assert.Empty(await services
                .GetRequiredService<AppDbContext>()
                .FeedbackMessages.ToListAsync());
        });
    }

    [Fact]
    public async Task AnalystInbox_HasNoFeedbackActions()
    {
        await WithServicesAsync(async services =>
        {
            await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.UnderAnalystReview,
                analystId: 11);
            await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.Approved,
                analystId: 11);
            await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.Rejected,
                analystId: 11);
        });
        using HttpClient client = CreateClient().AuthenticateAs(
            11,
            AppRoles.Analyst,
            TestDataSeeder.UniqueEmail("feedback-inbox-analyst"));

        string html = await client.GetStringAsync("/Analyst/Inbox");

        Assert.Contains("İncelemeye Devam Et", html);
        Assert.DoesNotContain("Geri Bildirim", html);
    }

    private static Task<ApplicationUser> CreateParticipantAsync(
        IServiceProvider services,
        int userId,
        string role,
        string fullName)
    {
        return TestDataSeeder.CreateUserAsync(
            services,
            TestDataSeeder.UniqueEmail($"feedback-{userId}"),
            assignedRole: role,
            userId: userId,
            fullName: fullName);
    }
}
