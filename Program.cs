using FlowDesk.Data;
using Microsoft.EntityFrameworkCore;
using FlowDesk.Repositories;
using FlowDesk.Repositories.Interfaces;
using FlowDesk.Services;
using FlowDesk.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

string connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "DefaultConnection baðlantý bilgisi bulunamadý."
    );

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString)

);
builder.Services.AddScoped<IWorkItemRepository, WorkItemRepository>();

builder.Services.AddScoped<IAnalystWorkflowService, AnalystWorkflowService>();
var app = builder.Build();

// HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.Run();