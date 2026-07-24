using FlowDesk.Data;
using FlowDesk.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlowDesk.CharacterizationTests.Infrastructure;

public sealed class FlowDeskWebApplicationFactory
    : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public FlowDeskWebApplicationFactory()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
    }

    public FakeEmailService Email =>
        Services.GetRequiredService<FakeEmailService>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

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
                options.UseSqlite(_connection));

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