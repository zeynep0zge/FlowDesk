using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlowDesk.Ai.Gemini.Models;
using FlowDesk.Ai.Gemini.Schemas;
using FlowDesk.Ai.Interfaces;
using FlowDesk.Ai.Models;
using FlowDesk.Ai.Options;
using FlowDesk.Ai.Prompts;
using FlowDesk.Common;
using Microsoft.Extensions.Options;

namespace FlowDesk.Ai.Gemini.Services;

public sealed class GeminiUnresolvedTermResearchService
    : IUnresolvedTermResearchService
{
    private const int MaximumTermCount = 10;
    private const int MaximumTermLength = 100;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;

    public GeminiUnresolvedTermResearchService(
        HttpClient httpClient,
        IOptions<GeminiOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<ServiceResult<UnresolvedTermResearchResult>>
        ResearchAsync(
            IReadOnlyCollection<string> unresolvedTerms,
            CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> normalizedTerms = NormalizeTerms(
            unresolvedTerms);

        if (normalizedTerms.Count == 0)
        {
            return ServiceResult<UnresolvedTermResearchResult>.Failure(
                "Araştırılacak geçerli terim bulunamadı.");
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return ServiceResult<UnresolvedTermResearchResult>.Failure(
                "Gemini API anahtarı yapılandırılmamış.");
        }

        if (string.IsNullOrWhiteSpace(_options.Model))
        {
            return ServiceResult<UnresolvedTermResearchResult>.Failure(
                "Gemini model bilgisi yapılandırılmamış.");
        }

        if (!TryCreateEndpoint(out Uri? endpoint))
        {
            return ServiceResult<UnresolvedTermResearchResult>.Failure(
                "Gemini servis adresi geçersiz.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(
                CreateRequest(normalizedTerms),
                options: JsonOptions)
        };
        request.Headers.Add("x-goog-api-key", _options.ApiKey);

        using var timeoutTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
        timeoutTokenSource.CancelAfter(TimeSpan.FromSeconds(
            Math.Clamp(_options.TimeoutSeconds, 1, 120)));

        try
        {
            using HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutTokenSource.Token);

            if (!response.IsSuccessStatusCode)
            {
                return ServiceResult<UnresolvedTermResearchResult>.Failure(
                    GetHttpErrorMessage(response.StatusCode));
            }

            string responseBody = await response.Content.ReadAsStringAsync(
                timeoutTokenSource.Token);
            GeminiGenerateContentResponse? geminiResponse =
                JsonSerializer.Deserialize<GeminiGenerateContentResponse>(
                    responseBody,
                    JsonOptions);

            GeminiCandidate? candidate = geminiResponse?.Candidates
                .FirstOrDefault(item => item.Content?.Parts.Any(part =>
                    !string.IsNullOrWhiteSpace(part.Text)) == true);
            string? responseJson = candidate?.Content?.Parts
                .FirstOrDefault(part =>
                    !string.IsNullOrWhiteSpace(part.Text))?.Text;

            if (string.IsNullOrWhiteSpace(responseJson))
            {
                return ServiceResult<UnresolvedTermResearchResult>.Failure(
                    "Gemini geçerli bir araştırma sonucu döndürmedi.");
            }

            UnresolvedTermResearchResult? parsedResult =
                JsonSerializer.Deserialize<UnresolvedTermResearchResult>(
                    responseJson,
                    JsonOptions);
            if (parsedResult?.Terms == null)
            {
                return ServiceResult<UnresolvedTermResearchResult>.Failure(
                    "Gemini araştırma yanıtı beklenen biçimde değil.");
            }

            IReadOnlyList<GroundingSource> sources = ExtractSources(candidate);
            IReadOnlyList<ResearchedTerm> terms = NormalizeResultTerms(
                normalizedTerms,
                parsedResult.Terms,
                sources.Count > 0);

            return ServiceResult<UnresolvedTermResearchResult>.Success(
                new UnresolvedTermResearchResult
                {
                    Terms = terms,
                    Sources = sources
                });
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return ServiceResult<UnresolvedTermResearchResult>.Failure(
                "Gemini araştırma isteği zaman aşımına uğradı.");
        }
        catch (HttpRequestException)
        {
            return ServiceResult<UnresolvedTermResearchResult>.Failure(
                "Gemini araştırma servisine bağlantı kurulamadı.");
        }
        catch (JsonException)
        {
            return ServiceResult<UnresolvedTermResearchResult>.Failure(
                "Gemini araştırma yanıtı okunabilir JSON biçiminde değil.");
        }
    }

    private static IReadOnlyList<string> NormalizeTerms(
        IReadOnlyCollection<string>? terms)
    {
        if (terms == null)
        {
            return [];
        }

        return terms
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Select(term => term.Trim())
            .Select(term => term.Length <= MaximumTermLength
                ? term
                : term[..MaximumTermLength])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaximumTermCount)
            .ToArray();
    }

    private static GeminiGenerateContentRequest CreateRequest(
        IReadOnlyList<string> terms)
    {
        string termsJson = JsonSerializer.Serialize(terms, JsonOptions);

        return new GeminiGenerateContentRequest
        {
            SystemInstruction = new GeminiRequestContent
            {
                Parts =
                [
                    new GeminiRequestPart
                    {
                        Text = UnresolvedTermResearchPrompt.SystemInstruction
                    }
                ]
            },
            Contents =
            [
                new GeminiRequestContent
                {
                    Role = "user",
                    Parts =
                    [
                        new GeminiRequestPart
                        {
                            Text = $"Araştırılacak terimler: {termsJson}"
                        }
                    ]
                }
            ],
            GenerationConfig = new GeminiGenerationConfig
            {
                ResponseFormat = new GeminiResponseFormat
                {
                    Text = new GeminiTextResponseFormat
                    {
                        MimeType = "APPLICATION_JSON",
                        Schema = UnresolvedTermResearchSchema.Create()
                    }
                }
            },
            Tools =
            [
                new GeminiTool
                {
                    GoogleSearch = new GeminiGoogleSearch()
                }
            ]
        };
    }

    private bool TryCreateEndpoint(out Uri? endpoint)
    {
        endpoint = null;
        string normalizedBaseUrl = _options.BaseUrl.TrimEnd('/') + "/";
        if (!Uri.TryCreate(
                normalizedBaseUrl,
                UriKind.Absolute,
                out Uri? baseUri))
        {
            return false;
        }

        string modelName = Uri.EscapeDataString(_options.Model.Trim());
        endpoint = new Uri(
            baseUri,
            $"models/{modelName}:generateContent");
        return true;
    }

    private static IReadOnlyList<GroundingSource> ExtractSources(
        GeminiCandidate candidate)
    {
        IReadOnlyList<GeminiGroundingChunk>? chunks =
            candidate.GroundingMetadata?.GroundingChunks;
        if (chunks == null)
        {
            return [];
        }

        return chunks
            .Select(chunk => chunk.Web)
            .Where(web => web != null &&
                TryGetAbsoluteWebUri(web.Uri, out _))
            .Select(web =>
            {
                TryGetAbsoluteWebUri(web!.Uri, out Uri? uri);
                return new GroundingSource
                {
                    Title = string.IsNullOrWhiteSpace(web.Title)
                        ? uri!.Host
                        : web.Title.Trim(),
                    Url = uri!.AbsoluteUri
                };
            })
            .DistinctBy(source => source.Url, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool TryGetAbsoluteWebUri(string? value, out Uri? uri)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out uri) &&
            (uri.Scheme == Uri.UriSchemeHttp ||
             uri.Scheme == Uri.UriSchemeHttps);
    }

    private static IReadOnlyList<ResearchedTerm> NormalizeResultTerms(
        IReadOnlyList<string> requestedTerms,
        IReadOnlyList<ResearchedTerm> returnedTerms,
        bool hasGroundingSource)
    {
        var returnedByTerm = returnedTerms
            .Where(term => !string.IsNullOrWhiteSpace(term.Term))
            .GroupBy(term => term.Term.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);

        return requestedTerms.Select(term =>
        {
            returnedByTerm.TryGetValue(term, out ResearchedTerm? returned);
            string? expandedForm = NormalizeOptional(returned?.ExpandedForm);
            string? turkishMeaning = NormalizeOptional(
                returned?.TurkishMeaning);
            string? explanation = NormalizeOptional(returned?.Explanation);
            bool hasExplanationOrExpansion =
                expandedForm != null || turkishMeaning != null ||
                explanation != null;

            return new ResearchedTerm
            {
                Term = term,
                IsResolved = returned?.IsResolved == true &&
                    hasExplanationOrExpansion && hasGroundingSource,
                ExpandedForm = expandedForm,
                TurkishMeaning = turkishMeaning,
                Explanation = explanation
            };
        }).ToArray();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string GetHttpErrorMessage(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                => "Gemini API anahtarı geçersiz veya yetkisiz.",
            HttpStatusCode.TooManyRequests
                => "Gemini kullanım sınırına ulaşıldı. Daha sonra tekrar deneyin.",
            HttpStatusCode.BadRequest
                => "Gemini araştırma isteği geçersiz bulundu.",
            _ => "Gemini araştırma servisi isteği tamamlayamadı."
        };
    }
}
