using FlowDesk.CharacterizationTests.Infrastructure;
using FlowDesk.Constants;
using FlowDesk.Data;
using FlowDesk.Models;
using FlowDesk.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.CharacterizationTests.Identifiers;

public sealed class IdentifierGenerationTests : DatabaseTestBase
{
    [Theory]
    [InlineData(AppRoles.Analyst, "ANL")]
    [InlineData(AppRoles.Employee, "ENG")]
    [InlineData(AppRoles.DepartmentManager, "DYN")]
    [InlineData(AppRoles.ProjectManager, "ISB")]
    public async Task GenerateUserCode_KnownRole_UsesExpectedFormat(
        string role,
        string prefix)
    {
        string code = await WithServicesAsync(async services =>
            await services.GetRequiredService<IIdentifierGenerator>()
                .GenerateUserCodeAsync(role));

        Assert.Matches($@"^{prefix}-\d{{8}}-\d{{4}}$", code);
    }

    [Fact]
    public async Task GenerateWorkItemCode_PersistedCodes_AreUnique()
    {
        HashSet<string> generatedCodes = [];

        await WithServicesAsync(async services =>
        {
            IIdentifierGenerator generator =
                services.GetRequiredService<IIdentifierGenerator>();
            AppDbContext context =
                services.GetRequiredService<AppDbContext>();

            for (int index = 0; index < 20; index++)
            {
                string code = await generator.GenerateWorkItemCodeAsync();
                generatedCodes.Add(code);
                context.WorkItems.Add(new WorkItem
                {
                    RequestNumber = code,
                    RequestDescription = "Identifier uniqueness test",
                    Department = TestDataSeeder.DefaultDepartment,
                    CreatedAt = DateTime.UtcNow
                });
                await context.SaveChangesAsync();
            }
        });

        Assert.Equal(20, generatedCodes.Count);
        Assert.All(generatedCodes, code =>
            Assert.Matches(@"^TLP-\d{8}-\d{4}$", code));
    }

    [Fact]
    public async Task GenerateUserCode_AfterPersistence_DoesNotRepeatCode()
    {
        await WithServicesAsync(async services =>
        {
            IIdentifierGenerator generator =
                services.GetRequiredService<IIdentifierGenerator>();
            string firstCode =
                await generator.GenerateUserCodeAsync(AppRoles.Analyst);

            await TestDataSeeder.CreateUserAsync(
                services,
                TestDataSeeder.UniqueEmail("identifier-user"),
                businessCode: firstCode);

            string secondCode =
                await generator.GenerateUserCodeAsync(AppRoles.Analyst);

            Assert.NotEqual(firstCode, secondCode);
        });
    }

    [Fact]
    public async Task Backfill_EmployeeWithoutCode_AssignsEngCode()
    {
        ApplicationUser employee = await WithServicesAsync(services =>
            TestDataSeeder.CreateUserAsync(
                services,
                TestDataSeeder.UniqueEmail("backfill-employee"),
                assignedRole: AppRoles.Employee));

        await WithServicesAsync(IdentitySeeder.BackfillBusinessCodesAsync);

        string? code = await WithServicesAsync(async services =>
            await services.GetRequiredService<AppDbContext>()
                .Users.AsNoTracking()
                .Where(user => user.Id == employee.Id)
                .Select(user => user.BusinessCode)
                .SingleAsync());

        Assert.Matches(@"^ENG-\d{8}-\d{4}$", code!);
    }

    [Fact]
    public async Task Backfill_UserWithoutResolvableRole_LeavesCodeNull()
    {
        ApplicationUser user = await WithServicesAsync(services =>
            TestDataSeeder.CreateUserAsync(
                services,
                TestDataSeeder.UniqueEmail("backfill-no-role")));

        await WithServicesAsync(IdentitySeeder.BackfillBusinessCodesAsync);

        string? code = await WithServicesAsync(async services =>
            await services.GetRequiredService<AppDbContext>()
                .Users.AsNoTracking()
                .Where(candidate => candidate.Id == user.Id)
                .Select(candidate => candidate.BusinessCode)
                .SingleAsync());

        Assert.Null(code);
    }
}
