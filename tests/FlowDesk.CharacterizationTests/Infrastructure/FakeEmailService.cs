using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using FlowDesk.Services.Interfaces;

namespace FlowDesk.CharacterizationTests.Infrastructure;

public sealed record SentEmail(string Recipient, string Subject, string Body);

public sealed class FakeEmailService : IEmailService
{
    private static readonly Regex SixDigitCodePattern = new(
        @"(?<!\d)\d{6}(?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ConcurrentQueue<SentEmail> _messages = new();

    public IReadOnlyList<SentEmail> Messages => _messages.ToArray();

    public int SendCount => _messages.Count;

    public Task SendAsync(
        string recipientEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _messages.Enqueue(new SentEmail(recipientEmail, subject, htmlBody));
        return Task.CompletedTask;
    }

    public string GetLatestSixDigitCode(string recipientEmail)
    {
        SentEmail? message = _messages.LastOrDefault(x => string.Equals(
            x.Recipient,
            recipientEmail,
            StringComparison.OrdinalIgnoreCase));

        if (message == null)
        {
            throw new InvalidOperationException(
                "Test alıcısı için e-posta bulunamadı.");
        }

        Match match = SixDigitCodePattern.Match(message.Body);

        if (!match.Success)
        {
            throw new InvalidOperationException(
                "E-posta gövdesinde altı haneli kod bulunamadı.");
        }

        return match.Value;
    }

    public void Clear()
    {
        while (_messages.TryDequeue(out _))
        {
        }
    }
}