using System.Net;
using System.Text;
using System.Text.Json;
using FlowDesk.Ai.Gemini.Services;
using FlowDesk.Ai.Options;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FlowDesk.CharacterizationTests.Ai;

public sealed class GeminiAnalystRequestRewriteServiceTests
{
    [Fact]
    public async Task RewriteAsync_ValidResponse_ReturnsStructuredResult()
    {
        // Gemini'nin text alanında döndüreceği iç JSON.
        var structuredResultJson = JsonSerializer.Serialize(new
        {
            rewrittenRequest =
                "ATM para çekme işlemindeki bekleme süresinin azaltılması talep edilmektedir.",

            abbreviations = new[]
            {
                new
                {
                    abbreviation = "ATM",
                    expandedForm = "Automated Teller Machine",
                    explanation = "Otomatik vezne makinesi."
                }
            },

            ambiguities = new[]
            {
                "Hangi ATM modellerinin etkilendiği belirtilmemiştir."
            },

            unresolvedTerms = Array.Empty<string>()
        });

        // Gemini API'nin dış response yapısı.
        var geminiResponseJson = JsonSerializer.Serialize(new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        parts = new[]
                        {
                            new
                            {
                                text = structuredResultJson
                            }
                        }
                    },

                    finishReason = "STOP"
                }
            }
        });

        string? capturedRequestBody = null;
        string? capturedApiKey = null;
        Uri? capturedRequestUri = null;

        var handler = new StubHttpMessageHandler(
            async (request, cancellationToken) =>
            {
                capturedRequestUri = request.RequestUri;

                capturedRequestBody = request.Content is null
                    ? null
                    : await request.Content.ReadAsStringAsync(
                        cancellationToken);

                capturedApiKey = request.Headers
                    .GetValues("x-goog-api-key")
                    .Single();

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        geminiResponseJson,
                        Encoding.UTF8,
                        "application/json")
                };
            });

        using var httpClient = new HttpClient(handler);

        var options = Microsoft.Extensions.Options.Options.Create(new GeminiOptions
        {
            ApiKey = "test-api-key",
            Model = "gemini-3.6-flash",
            BaseUrl =
                "https://generativelanguage.googleapis.com/v1beta/",
            TimeoutSeconds = 5
        });

        var service = new GeminiAnalystRequestRewriteService(
            httpClient,
            options,
            new RecordingLogger<GeminiAnalystRequestRewriteService>(),
            CreateEnvironment(Environments.Development));

        var result = await service.RewriteAsync(
            "atm para cekmede sure cok uzun duzeltilecek");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);

        Assert.Equal(
            "ATM para çekme işlemindeki bekleme süresinin azaltılması talep edilmektedir.",
            result.Data.RewrittenRequest);

        Assert.Single(result.Data.Abbreviations);
        Assert.Single(result.Data.Ambiguities);
        Assert.Empty(result.Data.UnresolvedTerms);

        Assert.Equal("test-api-key", capturedApiKey);

        Assert.Contains(
            "models/gemini-3.6-flash:generateContent",
            capturedRequestUri?.ToString());

        Assert.Contains(
            "\"systemInstruction\"",
            capturedRequestBody);

        Assert.Contains(
            "\"generationConfig\"",
            capturedRequestBody);

        Assert.Contains(
            "\"responseFormat\"",
            capturedRequestBody);

        Assert.Contains(
            "\"APPLICATION_JSON\"",
            capturedRequestBody);
    }

    [Fact]
    public async Task RewriteAsync_GeminiBadRequest_ParsesAndSafelyLogsError()
    {
        const string apiKey = "test-api-key-must-not-leak";
        const string rawOnlyMarker = "raw-response-must-not-reach-user";

        var errorResponseJson = JsonSerializer.Serialize(new
        {
            error = new
            {
                code = 400,
                message =
                    "Invalid response schema. Credential: " + apiKey,
                status = "INVALID_ARGUMENT",
                diagnostic = rawOnlyMarker
            }
        });

        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(
                        errorResponseJson,
                        Encoding.UTF8,
                        "application/json")
                }));

        using var httpClient = new HttpClient(handler);
        var logger =
            new RecordingLogger<GeminiAnalystRequestRewriteService>();

        var options = Microsoft.Extensions.Options.Options.Create(new GeminiOptions
        {
            ApiKey = apiKey,
            Model = "gemini-3.6-flash",
            BaseUrl =
                "https://generativelanguage.googleapis.com/v1beta/",
            TimeoutSeconds = 5
        });

        var service = new GeminiAnalystRequestRewriteService(
            httpClient,
            options,
            logger,
            CreateEnvironment(Environments.Development));

        var result = await service.RewriteAsync("Güvenli test talebi");

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "Gemini çıktı şeması geçersiz bulundu.",
            result.ErrorMessage);
        Assert.DoesNotContain(apiKey, result.ErrorMessage);
        Assert.DoesNotContain(rawOnlyMarker, result.ErrorMessage);

        var warning = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Contains("HTTP status code: 400", warning.Message);
        Assert.Contains("Gemini error status: INVALID_ARGUMENT", warning.Message);
        Assert.Contains(
            "Gemini error message: Invalid response schema. Credential: [REDACTED]",
            warning.Message);
        Assert.DoesNotContain(apiKey, warning.Message);
        Assert.DoesNotContain(rawOnlyMarker, warning.Message);
    }

    [Fact]
    public async Task RewriteAsync_EmptyDescription_ReturnsFailureWithoutHttpCall()
    {
        var requestWasSent = false;

        var handler = new StubHttpMessageHandler(
            (_, _) =>
            {
                requestWasSent = true;

                return Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK));
            });

        using var httpClient = new HttpClient(handler);

        var options = Microsoft.Extensions.Options.Options.Create(new GeminiOptions
        {
            ApiKey = "test-api-key",
            Model = "gemini-3.6-flash",
            BaseUrl =
                "https://generativelanguage.googleapis.com/v1beta/",
            TimeoutSeconds = 5
        });

        var service = new GeminiAnalystRequestRewriteService(
            httpClient,
            options,
            new RecordingLogger<GeminiAnalystRequestRewriteService>(),
            CreateEnvironment(Environments.Development));

        var result = await service.RewriteAsync("   ");

        Assert.False(result.IsSuccess);
        Assert.False(requestWasSent);

        Assert.Equal(
            "Düzenlenecek talep açıklaması boş olamaz.",
            result.ErrorMessage);
    }

    private static IHostEnvironment CreateEnvironment(
        string environmentName)
    {
        return new TestHostEnvironment
        {
            EnvironmentName = environmentName
        };
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<
            HttpRequestMessage,
            CancellationToken,
            Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(
            Func<
                HttpRequestMessage,
                CancellationToken,
                Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(
                logLevel,
                formatter(state, exception)));
        }
    }

    private sealed record LogEntry(
        LogLevel Level,
        string Message);

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } =
            Environments.Production;

        public string ApplicationName { get; set; } = "FlowDesk.Tests";

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
