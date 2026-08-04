using FlowDesk.Data;
using Microsoft.EntityFrameworkCore;
using FlowDesk.Repositories;
using FlowDesk.Repositories.Interfaces;
using FlowDesk.Services;
using FlowDesk.Services.Interfaces;
using FlowDesk.Models;
using Microsoft.AspNetCore.Identity;
using FlowDesk.Options;
using FlowDesk.Common;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using FlowDesk.Ai.Options;
using FlowDesk.Ai.Gemini.Services;
using FlowDesk.Ai.Interfaces;
using FlowDesk.Ai.Services;
using FlowDesk.Ai.Repositories;
using FlowDesk.Ai.Repositories.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

int accountRateLimit = Math.Max(
    1,
    builder.Configuration.GetValue<int?>(
        "RateLimiting:Account:PermitLimit") ?? 5);
int accountRateLimitWindowMinutes = Math.Max(
    1,
    builder.Configuration.GetValue<int?>(
        "RateLimiting:Account:WindowMinutes") ?? 10);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType =
            "text/plain; charset=utf-8";
        await context.HttpContext.Response.WriteAsync(
            "Çok fazla istek gönderdiniz. Lütfen daha sonra tekrar deneyin.",
            cancellationToken);
    };

    options.AddPolicy(
        AccountRateLimitPolicies.Register,
        CreateAccountRateLimitPartitioner(
            accountRateLimit,
            accountRateLimitWindowMinutes));
    options.AddPolicy(
        AccountRateLimitPolicies.ForgotPassword,
        CreateAccountRateLimitPartitioner(
            accountRateLimit,
            accountRateLimitWindowMinutes));
    options.AddPolicy(
        AccountRateLimitPolicies.ResendEmailVerification,
        CreateAccountRateLimitPartitioner(
            accountRateLimit,
            accountRateLimitWindowMinutes));
});

string connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "DefaultConnection bağlantı bilgisi bulunamadı."
    );

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString)

);

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<int>>(options =>
    {
        options.SignIn.RequireConfirmedEmail = true;

        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));

builder.Services.AddScoped<IEmailService, BrevoEmailService>();
builder.Services.AddScoped<
    IAccountRegistrationService,
    AccountRegistrationService>();
builder.Services.AddScoped<
    IAccountEmailVerificationService,
    AccountEmailVerificationService>();
builder.Services.AddScoped<
    IAccountPasswordResetService,
    AccountPasswordResetService>();
builder.Services.AddScoped<
    IAccountAuthenticationService,
    AccountAuthenticationService>();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.AddScoped<IWorkItemRepository, WorkItemRepository>();
builder.Services.AddScoped<
    IWorkItemReadRepository,
    WorkItemReadRepository>();

builder.Services.AddScoped<IIdentifierGenerator, IdentifierGenerator>();

builder.Services.AddScoped<
    IProjectManagerWorkItemService,
    ProjectManagerWorkItemService>();

builder.Services.AddScoped<
    IEmployeeWorkItemService,
    EmployeeWorkItemService>();

builder.Services.AddScoped<
    IAccountApprovalService,
    AccountApprovalService>();

builder.Services.AddScoped<
    IManagerAccessScopeResolver,
    ManagerAccessScopeResolver>();

builder.Services.AddScoped<
    IAuthenticatedActorContextResolver,
    AuthenticatedActorContextResolver>();

builder.Services.AddScoped<
    IAuthorizedWorkItemQueryService,
    AuthorizedWorkItemQueryService>();

builder.Services.AddScoped<
    IAnalystAiWorkflowService,
    AnalystAiWorkflowService>();

builder.Services.AddScoped<
    IWorkItemAiDraftRepository,
    WorkItemAiDraftRepository>();

builder.Services.AddScoped<IAnalystWorkflowService, AnalystWorkflowService>();

builder.Services.AddScoped<
    IDepartmentManagerWorkflowService,
    DepartmentManagerWorkflowService>();

builder.Services.AddScoped<
    IExcelExportService,
    ExcelExportService>();

builder.Services.AddScoped<
    IPasswordHasher<PasswordResetRequest>,
    PasswordHasher<PasswordResetRequest>>();

builder.Services.AddScoped<
    IPasswordHasher<EmailVerificationRequest>,
    PasswordHasher<EmailVerificationRequest>>();

builder.Services.Configure<GeminiOptions>(
    builder.Configuration.GetSection(GeminiOptions.SectionName));

builder.Services.AddHttpClient<
    IAnalystRequestRewriteService,
    GeminiAnalystRequestRewriteService>(httpClient =>
    {
        httpClient.Timeout = Timeout.InfiniteTimeSpan;
    });

builder.Services.AddHttpClient<
    IUnresolvedTermResearchService,
    GeminiUnresolvedTermResearchService>(httpClient =>
    {
        httpClient.Timeout = Timeout.InfiniteTimeSpan;
    });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await IdentitySeeder.SeedRolesAsync(
        scope.ServiceProvider);

    if (app.Environment.IsDevelopment())
    {
        await IdentitySeeder.SeedTestUsersAsync(
            scope.ServiceProvider);
    }

    await IdentitySeeder.BackfillBusinessCodesAsync(
        scope.ServiceProvider);

}

// HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}"
);

if (app.Environment.IsDevelopment())
{
    app.MapGet(
        "/dev/send-test-email",
        async (
            IEmailService emailService,
            IConfiguration configuration) =>
        {
            var recipient =
                configuration["EmailSettings:TestRecipient"];

            if (string.IsNullOrWhiteSpace(recipient))
            {
                return Results.BadRequest(
                    "TestRecipient ayarı bulunamadı.");
            }

            await emailService.SendAsync(
                recipient,
                "FlowDesk SMTP Testi",
                """
                <div style="font-family:Arial,sans-serif">
                    <h2>FlowDesk</h2>
                    <p>Brevo SMTP bağlantısı başarıyla çalışıyor.</p>
                </div>
                """);

            return Results.Ok(
                "Test e-postası gönderildi.");
        })
        .RequireAuthorization();
}

app.Run();

static Func<HttpContext, RateLimitPartition<string>>
    CreateAccountRateLimitPartitioner(
        int permitLimit,
        int windowMinutes)
{
    return httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromMinutes(windowMinutes),
            QueueLimit = 0,
            AutoReplenishment = true
        });
}

public partial class Program
{
}
