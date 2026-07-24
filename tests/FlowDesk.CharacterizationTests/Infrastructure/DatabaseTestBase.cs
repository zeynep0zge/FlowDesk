using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FlowDesk.CharacterizationTests.Infrastructure;

public abstract class DatabaseTestBase : IAsyncLifetime
{
    protected FlowDeskWebApplicationFactory Factory { get; } = new();

    public Task InitializeAsync()
    {
        return Factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        Factory.Dispose();
        return Task.CompletedTask;
    }

    protected HttpClient CreateClient()
    {
        return Factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost")
            });
    }

    protected async Task<TResult> WithServicesAsync<TResult>(
        Func<IServiceProvider, Task<TResult>> action)
    {
        await using AsyncServiceScope scope =
            Factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    protected async Task WithServicesAsync(
        Func<IServiceProvider, Task> action)
    {
        await using AsyncServiceScope scope =
            Factory.Services.CreateAsyncScope();
        await action(scope.ServiceProvider);
    }
}