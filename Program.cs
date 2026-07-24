using FlowDesk.Data;
using Microsoft.EntityFrameworkCore;
using FlowDesk.Repositories;
using FlowDesk.Repositories.Interfaces;
using FlowDesk.Services;
using FlowDesk.Services.Interfaces;
using FlowDesk.Models;
using Microsoft.AspNetCore.Identity;
using FlowDesk.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

string connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "DefaultConnection ba�lant� bilgisi bulunamad�."
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
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.AddScoped<IWorkItemRepository, WorkItemRepository>();

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
                    "TestRecipient ayar� bulunamad�.");
            }

            await emailService.SendAsync(
                recipient,
                "FlowDesk SMTP Testi",
                """
                <div style="font-family:Arial,sans-serif">
                    <h2>FlowDesk</h2>
                    <p>Brevo SMTP ba�lant�s� ba�ar�yla �al���yor.</p>
                </div>
                """);

            return Results.Ok(
                "Test e-postas� g�nderildi.");
        })
        .RequireAuthorization();
}

app.Run();

public partial class Program
{
}
