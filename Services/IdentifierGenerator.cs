using System.Globalization;
using System.Security.Cryptography;
using FlowDesk.Constants;
using FlowDesk.Data;
using FlowDesk.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Services;

public sealed class IdentifierGenerator : IIdentifierGenerator
{
    private const int MaxAttempts = 100;
    private readonly AppDbContext _context;

    public IdentifierGenerator(AppDbContext context)
    {
        _context = context;
    }

    public Task<string> GenerateUserCodeAsync(string role)
    {
        string? prefix = AppRoles.GetBusinessCodePrefix(role);
        if (prefix == null)
        {
            throw new ArgumentException(
                "Business code cannot be generated for this role.",
                nameof(role));
        }

        return GenerateUniqueCodeAsync(
            prefix,
            candidate => _context.Users.AnyAsync(
                user => user.BusinessCode == candidate));
    }

    public Task<string> GenerateWorkItemCodeAsync()
    {
        return GenerateUniqueCodeAsync(
            "TLP",
            candidate => _context.WorkItems.AnyAsync(
                workItem => workItem.RequestNumber == candidate));
    }

    private static async Task<string> GenerateUniqueCodeAsync(
        string prefix,
        Func<string, Task<bool>> codeExistsAsync)
    {
        string datePart = DateTime.UtcNow.ToString(
            "ddMMyyyy",
            CultureInfo.InvariantCulture);

        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            int randomPart = RandomNumberGenerator.GetInt32(0, 10000);
            string candidate = $"{prefix}-{datePart}-{randomPart:0000}";

            if (!await codeExistsAsync(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            $"A unique {prefix} identifier could not be generated.");
    }
}
