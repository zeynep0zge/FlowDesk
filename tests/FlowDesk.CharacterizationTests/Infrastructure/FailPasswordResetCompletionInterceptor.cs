using FlowDesk.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FlowDesk.CharacterizationTests.Infrastructure;

public sealed class FailPasswordResetCompletionInterceptor
    : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        bool completingReset = eventData.Context?.ChangeTracker
            .Entries<PasswordResetRequest>()
            .Any(entry =>
                entry.State == EntityState.Modified &&
                entry.Entity.CompletedAtUtc.HasValue) == true;

        if (completingReset)
        {
            throw new DbUpdateException(
                "Controlled password-reset completion failure.");
        }

        return base.SavingChangesAsync(
            eventData,
            result,
            cancellationToken);
    }
}
