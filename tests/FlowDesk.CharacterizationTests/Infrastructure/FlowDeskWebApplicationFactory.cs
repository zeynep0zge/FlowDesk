using FlowDesk.Data;
using FlowDesk.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace FlowDesk.CharacterizationTests.Infrastructure;

public sealed class FlowDeskWebApplicationFactory
    : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;
    private readonly string _environmentName;
    private readonly IInterceptor? _interceptor;

    public FlowDeskWebApplicationFactory(
        string environmentName = "Testing",
        IInterceptor? interceptor = null)
    {
        _environmentName = environmentName;
        _interceptor = interceptor;
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
    }

    public FakeEmailService Email =>
        Services.GetRequiredService<FakeEmailService>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environmentName);
        builder.ConfigureLogging(logging => logging.ClearProviders());

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        "Server=unused-by-characterization-tests;" +
                        "Database=unused;Trusted_Connection=True;",
                    ["EmailSettings:Host"] = "unused.test",
                    ["EmailSettings:Port"] = "587",
                    ["EmailSettings:SenderName"] = "FlowDesk Tests",
                    ["EmailSettings:SenderEmail"] = "no-reply@test.local",
                    ["EmailSettings:Username"] = "unused",
                    ["EmailSettings:Password"] = "unused"
                });
        });

        builder.ConfigureServices(services =>
        {
            services.AddDataProtection()
                .UseEphemeralDataProtectionProvider();

            services.RemoveAll<AppDbContext>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            foreach (var descriptor in services
                         .Where(x => x.ServiceType.FullName?.Contains(
                             "IDbContextOptionsConfiguration") == true)
                         .ToList())
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite(_connection);
                if (_interceptor != null)
                {
                    options.AddInterceptors(_interceptor);
                }
            });

            services.RemoveAll<IEmailService>();
            services.AddSingleton<FakeEmailService>();
            services.AddSingleton<IEmailService>(provider =>
                provider.GetRequiredService<FakeEmailService>());

            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme =
                        TestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme =
                        TestAuthenticationHandler.SchemeName;
                    options.DefaultForbidScheme =
                        TestAuthenticationHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    _ => { });

            // Program seeds Identity roles before the web host starts. Prepare the
            // in-memory schema here so that production startup behavior can run
            // unchanged against the test database.
            using ServiceProvider schemaProvider =
                services.BuildServiceProvider();
            using IServiceScope schemaScope =
                schemaProvider.CreateScope();
            schemaScope.ServiceProvider
                .GetRequiredService<AppDbContext>()
                .Database.EnsureCreated();
        });
    }

    public async Task ResetDatabaseAsync()
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        AppDbContext context =
            scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        await IdentitySeeder.SeedRolesAsync(scope.ServiceProvider);
        await TestDataSeeder.CreateUserAsync(
            scope.ServiceProvider,
            TestDataSeeder.UniqueEmail("default-employee"),
            assignedRole: FlowDesk.Constants.AppRoles.Employee,
            userId: 22);
        await TestDataSeeder.CreateUserAsync(
            scope.ServiceProvider,
            TestDataSeeder.UniqueEmail("default-manager"),
            assignedRole: FlowDesk.Constants.AppRoles.DepartmentManager,
            userId: 9001);
        await IdentitySeeder.BackfillBusinessCodesAsync(
            scope.ServiceProvider);
        Email.Clear();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
