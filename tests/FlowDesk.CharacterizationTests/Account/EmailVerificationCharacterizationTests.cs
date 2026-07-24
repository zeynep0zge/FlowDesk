using System.Net;
using FlowDesk.CharacterizationTests.Infrastructure;
using FlowDesk.Constants;
using FlowDesk.Data;
using FlowDesk.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.CharacterizationTests.Account;

public sealed class EmailVerificationCharacterizationTests
    : DatabaseTestBase
{
    [Fact]
    public async Task VerifyEmail_ValidCode_ConfirmsEmailAndInvalidatesRequest()
    {
        using HttpClient client = CreateClient();
        string email = TestDataSeeder.UniqueEmail("verify-valid");
        await RegisterAsync(client, email);
        string code = Factory.Email.GetLatestSixDigitCode(email);

        HttpResponseMessage response = await VerifyAsync(
            client,
            email,
            code);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(
            "/Account/PendingApproval",
            response.Headers.Location?.OriginalString,
            StringComparison.OrdinalIgnoreCase);

        var state = await WithServicesAsync(async services =>
        {
            AppDbContext context =
                services.GetRequiredService<AppDbContext>();
            ApplicationUser user = await context.Users
                .SingleAsync(x => x.Email == email);
            EmailVerificationRequest request =
                await context.EmailVerificationRequests
                    .SingleAsync(x => x.UserId == user.Id);
            return new
            {
                user.EmailConfirmed,
                user.IsApproved,
                request.VerifiedAtUtc,
                request.IsInvalidated
            };
        });

        Assert.True(state.EmailConfirmed);
        Assert.False(state.IsApproved);
        Assert.NotNull(state.VerifiedAtUtc);
        Assert.True(state.IsInvalidated);
    }

    [Fact]
    public async Task VerifyEmail_WrongCode_DoesNotConfirmEmail()
    {
        using HttpClient client = CreateClient();
        string email = TestDataSeeder.UniqueEmail("verify-wrong");
        await RegisterAsync(client, email);
        string actualCode = Factory.Email.GetLatestSixDigitCode(email);
        string wrongCode = actualCode == "000000" ? "111111" : "000000";

        HttpResponseMessage response = await VerifyAsync(
            client,
            email,
            wrongCode);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var state = await WithServicesAsync(async services =>
        {
            AppDbContext context =
                services.GetRequiredService<AppDbContext>();
            ApplicationUser user = await context.Users
                .SingleAsync(x => x.Email == email);
            EmailVerificationRequest request =
                await context.EmailVerificationRequests
                    .SingleAsync(x => x.UserId == user.Id);
            return new
            {
                user.EmailConfirmed,
                request.FailedAttemptCount
            };
        });

        Assert.False(state.EmailConfirmed);
        Assert.Equal(1, state.FailedAttemptCount);
    }

    [Fact]
    public async Task VerifyEmail_ExpiredCode_DoesNotConfirmEmail()
    {
        using HttpClient client = CreateClient();
        string email = TestDataSeeder.UniqueEmail("verify-expired");
        await RegisterAsync(client, email);
        string code = Factory.Email.GetLatestSixDigitCode(email);

        await WithServicesAsync(async services =>
        {
            AppDbContext context =
                services.GetRequiredService<AppDbContext>();
            EmailVerificationRequest request =
                await context.EmailVerificationRequests.SingleAsync();
            request.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
            await context.SaveChangesAsync();
        });

        HttpResponseMessage response = await VerifyAsync(
            client,
            email,
            code);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var state = await WithServicesAsync(async services =>
        {
            AppDbContext context =
                services.GetRequiredService<AppDbContext>();
            ApplicationUser user = await context.Users
                .SingleAsync(x => x.Email == email);
            EmailVerificationRequest request =
                await context.EmailVerificationRequests.SingleAsync();
            return new
            {
                user.EmailConfirmed,
                request.IsInvalidated
            };
        });

        Assert.False(state.EmailConfirmed);
        Assert.True(state.IsInvalidated);
    }

    [Fact]
    public async Task VerifyEmail_UsedCode_CannotBeUsedAgain()
    {
        using HttpClient client = CreateClient();
        string email = TestDataSeeder.UniqueEmail("verify-reuse");
        await RegisterAsync(client, email);
        string code = Factory.Email.GetLatestSixDigitCode(email);
        await VerifyAsync(client, email, code);

        await WithServicesAsync(async services =>
        {
            UserManager<ApplicationUser> userManager =
                services.GetRequiredService<
                    UserManager<ApplicationUser>>();
            ApplicationUser user =
                (await userManager.FindByEmailAsync(email))!;
            user.EmailConfirmed = false;
            IdentityResult result = await userManager.UpdateAsync(user);
            Assert.True(result.Succeeded);
        });

        HttpResponseMessage retry = await VerifyAsync(
            client,
            email,
            code);

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);

        bool confirmed = await WithServicesAsync(async services =>
            (await services.GetRequiredService<
                    UserManager<ApplicationUser>>()
                .FindByEmailAsync(email))!.EmailConfirmed);

        Assert.False(confirmed);
    }

    private static Task<HttpResponseMessage> RegisterAsync(
        HttpClient client,
        string email)
    {
        return client.PostFormWithAntiforgeryAsync(
            "/Account/Register",
            "/Account/Register",
            AccountTestHelper.ValidRegistration(
                email,
                AppRoles.ProjectManager));
    }

    private static Task<HttpResponseMessage> VerifyAsync(
        HttpClient client,
        string email,
        string code)
    {
        string encodedEmail = Uri.EscapeDataString(email);
        return client.PostFormWithAntiforgeryAsync(
            $"/Account/VerifyEmailCode?email={encodedEmail}",
            "/Account/VerifyEmailCode",
            new Dictionary<string, string>
            {
                ["Email"] = email,
                ["Code"] = code
            });
    }
}