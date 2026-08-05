using System.Net;
using System.Text;
using System.Text.Json;
using FlowDesk.Ai.Gemini.Services;
using FlowDesk.Ai.Models;
using FlowDesk.Ai.Options;

namespace FlowDesk.CharacterizationTests.Ai;

public sealed class GeminiUnresolvedTermResearchServiceTests
{
    [Fact]
    public async Task ResearchAsync_SendsGoogleSearchAndStructuredSchema()
    {
        string structuredResultJson = JsonSerializer.Serialize(new
        {
            terms = new[]
            {
                new
                {
                    term = "BKM",
                    isResolved = true,
                    expandedForm = "Bankalararası Kart Merkezi",
                    turkishMeaning = (string?)null,
                    explanation = "Kartlı ödeme sistemleri kuruluşu."
                }
            }
        });
        string geminiResponseJson = JsonSerializer.Serialize(new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        parts = new[] { new { text = structuredResultJson } }
                    },
                    groundingMetadata = new
                    {
                        groundingChunks = new object[]
                        {
                            new
                            {
                                web = new
                                {
                                    uri = "https://example.com/bkm",
                                    title = "BKM Kaynağı"
                                }
                            },
                            new
                            {
                                web = new
                                {
                                    uri = "https://example.com/bkm",
                                    title = "Aynı Kaynak"
                                }
                            },
                            new
                            {
                                web = new
                                {
                                    uri = "relative-url",
                                    title = "Geçersiz"
                                }
                            }
                        }
                    }
                }
            }
        });

        string? requestBody = null;
        string? apiKey = null;
        var handler = new StubHttpMessageHandler(
            async (request, cancellationToken) =>
            {
                requestBody = await request.Content!.ReadAsStringAsync(
                    cancellationToken);
                apiKey = request.Headers.GetValues("x-goog-api-key").Single();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        geminiResponseJson,
                        Encoding.UTF8,
                        "application/json")
                };
            });
        using var httpClient = new HttpClient(handler);
        var service = new GeminiUnresolvedTermResearchService(
            httpClient,
            Microsoft.Extensions.Options.Options.Create(new GeminiOptions
            {
                ApiKey = "test-api-key",
                Model = "gemini-test-model",
                BaseUrl =
                    "https://generativelanguage.googleapis.com/v1beta/",
                TimeoutSeconds = 5
            }));

        var result = await service.ResearchAsync(
            [" BKM ", "bkm"]);

        Assert.True(result.IsSuccess);
        Assert.Equal("test-api-key", apiKey);
        ResearchedTerm term = Assert.Single(result.Data!.Terms);
        Assert.Equal("BKM", term.Term);
        Assert.True(term.IsResolved);
        GroundingSource source = Assert.Single(result.Data.Sources);
        Assert.Equal("https://example.com/bkm", source.Url);

        using JsonDocument document = JsonDocument.Parse(requestBody!);
        JsonElement root = document.RootElement;
        Assert.Equal(
            JsonValueKind.Object,
            root.GetProperty("tools")[0]
                .GetProperty("googleSearch").ValueKind);
        JsonElement schema = root
            .GetProperty("generationConfig")
            .GetProperty("responseFormat")
            .GetProperty("text")
            .GetProperty("schema");
        Assert.Equal("object", schema.GetProperty("type").GetString());
        Assert.Equal(
            "array",
            schema.GetProperty("properties")
                .GetProperty("terms")
                .GetProperty("type")
                .GetString());
        string termPayload = root.GetProperty("contents")[0]
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString()!;
        Assert.Contains("BKM", termPayload);
        Assert.DoesNotContain("WorkItem", termPayload);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken,
            Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(
            Func<HttpRequestMessage, CancellationToken,
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
}
