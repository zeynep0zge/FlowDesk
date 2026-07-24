using FlowDesk.Data;
using FlowDesk.Models;
using Microsoft.AspNetCore.Identity;

namespace FlowDesk.CharacterizationTests.Infrastructure;

public static class TestDataSeeder
{
    public const string DefaultPassword = "FlowDesk123!";
    public const string DefaultDepartment = "Kartlı Sistemler 1";

    public static string UniqueEmail(string prefix)
    {
        return $"{prefix}-{Guid.NewGuid():N}@test.local";
    }

    public static string UniqueRequestNumber(string prefix)
    {
        return $"{prefix}-{Guid.NewGuid():N}";
    }

    public static async Task<ApplicationUser> CreateUserAsync(
        IServiceProvider services,
        string email,
        bool emailConfirmed = true,
        bool isApproved = true,
        string? requestedRole = null,
        string? assignedRole = null)
    {
        UserManager<ApplicationUser> userManager =
            services.GetRequiredService<UserManager<ApplicationUser>>();

        ApplicationUser user = new()
        {
            UserName = email,
            Email = email,
            FullName = "Characterization Test User",
            Department = DefaultDepartment,
            RequestedRole = requestedRole,
            EmailConfirmed = emailConfirmed,
            IsApproved = isApproved,
            CreatedAtUtc = DateTime.UtcNow
        };

        IdentityResult result = await userManager.CreateAsync(
            user,
            DefaultPassword);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Test kullanıcısı oluşturulamadı.");
        }

        if (!string.IsNullOrWhiteSpace(assignedRole))
        {
            IdentityResult roleResult = await userManager.AddToRoleAsync(
                user,
                assignedRole);

            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Test kullanıcısına rol atanamadı.");
            }
        }

        return user;
    }

    public static async Task<WorkItem> CreateWorkItemAsync(
        IServiceProvider services,
        WorkflowStatus workflowStatus,
        int? createdByUserId = 101)
    {
        AppDbContext context =
            services.GetRequiredService<AppDbContext>();

        WorkItem workItem = new()
        {
            RequestNumber = UniqueRequestNumber("REQ"),
            RequestDescription = "Characterization test request",
            Department = DefaultDepartment,
            Priority = RequestPriority.Normal,
            CreatedByUserId = createdByUserId,
            WorkflowStatus = workflowStatus,
            CurrentStatus = workflowStatus.ToString(),
            CreatedAt = DateTime.UtcNow
        };

        context.WorkItems.Add(workItem);
        await context.SaveChangesAsync();
        return workItem;
    }
}