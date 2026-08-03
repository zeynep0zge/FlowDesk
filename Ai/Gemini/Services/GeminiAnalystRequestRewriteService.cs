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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FlowDesk.Ai.Gemini.Services;

public sealed class GeminiAnalystRequestRewriteService
    : IAnalystRequestRewriteService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiAnalystRequestRewriteService> _logger;
    private readonly IHostEnvironment _hostEnvironment;

    public GeminiAnalystRequestRewriteService(
        HttpClient httpClient,
        IOptions<GeminiOptions> options,
        ILogger<GeminiAnalystRequestRewriteService> logger,
        IHostEnvironment hostEnvironment)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _hostEnvironment = hostEnvironment;
    }

    public async Task<ServiceResult<AnalystRequestRewriteResult>> RewriteAsync(
        string originalRequestDescription,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(originalRequestDescription))
        {
            return ServiceResult<AnalystRequestRewriteResult>.Failure(
                "Düzenlenecek talep açıklaması boş olamaz.");
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return ServiceResult<AnalystRequestRewriteResult>.Failure(
                "Gemini API anahtarı yapılandırılmamış.");
        }

        if (string.IsNullOrWhiteSpace(_options.Model))
        {
            return ServiceResult<AnalystRequestRewriteResult>.Failure(
                "Gemini model bilgisi yapılandırılmamış.");
        }

        if (!TryCreateEndpoint(out var endpoint))
        {
            return ServiceResult<AnalystRequestRewriteResult>.Failure(
                "Gemini servis adresi geçersiz.");
        }

        var normalizedRequest = originalRequestDescription.Trim();

        var geminiRequest = CreateRequest(normalizedRequest);

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            endpoint)
        {
            Content = JsonContent.Create(
                geminiRequest,
                options: JsonOptions)
        };

        httpRequest.Headers.Add(
            "x-goog-api-key",
            _options.ApiKey);

        using var timeoutTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        var timeoutSeconds = Math.Clamp(
            _options.TimeoutSeconds,
            1,
            120);

        timeoutTokenSource.CancelAfter(
            TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            using var response = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutTokenSource.Token);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(
                    timeoutTokenSource.Token);

                var geminiError = TryReadError(responseBody);
                var safeErrorMessage = SanitizeErrorMessage(
                    geminiError?.Message,
                    normalizedRequest);

                _logger.LogWarning(
                    "Gemini request failed. HTTP status code: {HttpStatusCode}; " +
                    "Gemini error status: {GeminiErrorStatus}; " +
                    "Gemini error message: {GeminiErrorMessage}",
                    (int)response.StatusCode,
                    geminiError?.Status,
                    safeErrorMessage);

                return ServiceResult<AnalystRequestRewriteResult>.Failure(
                    GetHttpErrorMessage(
                        response.StatusCode,
                        geminiError));
            }

            var geminiResponse =
                await response.Content
                    .ReadFromJsonAsync<GeminiGenerateContentResponse>(
                        JsonOptions,
                        timeoutTokenSource.Token);

            var responseJson = ExtractResponseText(geminiResponse);

            if (string.IsNullOrWhiteSpace(responseJson))
            {
                return ServiceResult<AnalystRequestRewriteResult>.Failure(
                    "Gemini geçerli bir analiz sonucu döndürmedi.");
            }

            var rewriteResult =
                JsonSerializer.Deserialize<AnalystRequestRewriteResult>(
                    responseJson,
                    JsonOptions);

            if (rewriteResult is null ||
                string.IsNullOrWhiteSpace(
                    rewriteResult.RewrittenRequest))
            {
                return ServiceResult<AnalystRequestRewriteResult>.Failure(
                    "Gemini yanıtı beklenen biçimde değil.");
            }

            return ServiceResult<AnalystRequestRewriteResult>.Success(
                rewriteResult);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return ServiceResult<AnalystRequestRewriteResult>.Failure(
                "Gemini isteği zaman aşımına uğradı.");
        }
        catch (HttpRequestException)
        {
            return ServiceResult<AnalystRequestRewriteResult>.Failure(
                "Gemini servisine bağlantı kurulamadı.");
        }
        catch (JsonException)
        {
            return ServiceResult<AnalystRequestRewriteResult>.Failure(
                "Gemini yanıtı okunabilir JSON biçiminde değil.");
        }
    }

    private GeminiGenerateContentRequest CreateRequest(
        string originalRequestDescription)
    {
        var userContent = $"""
            Aşağıdaki orijinal talep metnini sistem talimatlarına göre düzenle.

            <original-request>
            {originalRequestDescription}
            </original-request>
            """;

        return new GeminiGenerateContentRequest
        {
            SystemInstruction = new GeminiRequestContent
            {
                Parts =
                [
                    new GeminiRequestPart
                    {
                        Text =
                            AnalystRequestRewritePrompt.SystemInstruction
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
                            Text = userContent
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

                        Schema =
                            AnalystRequestRewriteSchema.Create()
                    }
                }
            }
        };
    }

    private bool TryCreateEndpoint(out Uri? endpoint)
    {
        endpoint = null;

        var normalizedBaseUrl =
            _options.BaseUrl.TrimEnd('/') + "/";

        if (!Uri.TryCreate(
                normalizedBaseUrl,
                UriKind.Absolute,
                out var baseUri))
        {
            return false;
        }

        var modelName =
            Uri.EscapeDataString(_options.Model.Trim());

        endpoint = new Uri(
            baseUri,
            $"models/{modelName}:generateContent");

        return true;
    }

    private static string? ExtractResponseText(
        GeminiGenerateContentResponse? response)
    {
        return response?
            .Candidates
            .FirstOrDefault()?
            .Content?
            .Parts
            .FirstOrDefault(part =>
                !string.IsNullOrWhiteSpace(part.Text))?
            .Text;
    }

    private static GeminiErrorDetails? TryReadError(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<GeminiErrorResponse>(
                responseBody,
                JsonOptions)?.Error;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private string? SanitizeErrorMessage(
        string? errorMessage,
        string originalRequestDescription)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return errorMessage;
        }

        var sanitizedMessage = errorMessage;

        sanitizedMessage = ReplaceSensitiveValue(
            sanitizedMessage,
            _options.ApiKey);

        sanitizedMessage = ReplaceSensitiveValue(
            sanitizedMessage,
            originalRequestDescription);

        sanitizedMessage = ReplaceSensitiveValue(
            sanitizedMessage,
            AnalystRequestRewritePrompt.SystemInstruction);

        return sanitizedMessage;
    }

    private static string ReplaceSensitiveValue(
        string value,
        string? sensitiveValue)
    {
        return string.IsNullOrWhiteSpace(sensitiveValue)
            ? value
            : value.Replace(
                sensitiveValue,
                "[REDACTED]",
                StringComparison.Ordinal);
    }

    private string GetHttpErrorMessage(
        HttpStatusCode statusCode,
        GeminiErrorDetails? geminiError)
    {
        var generalMessage = statusCode switch
        {
            HttpStatusCode.Unauthorized or
            HttpStatusCode.Forbidden
                => "Gemini API anahtarı geçersiz veya yetkisiz.",

            HttpStatusCode.TooManyRequests
                => "Gemini kullanım sınırına ulaşıldı. Daha sonra tekrar deneyin.",

            HttpStatusCode.BadRequest
                => "Gemini isteği geçersiz bulundu.",

            _ => "Gemini servisi isteği tamamlayamadı."
        };

        if (!_hostEnvironment.IsDevelopment())
        {
            return generalMessage;
        }

        if (IsInvalidApiKey(statusCode, geminiError))
        {
            return "Gemini API anahtarı geçersiz.";
        }

        if (IsUnavailableModel(geminiError))
        {
            return "Yapılandırılan Gemini modeli kullanılamıyor.";
        }

        if (ContainsAny(
                geminiError?.Message,
                "response schema",
                "response_schema",
                "response_format.text.schema"))
        {
            return "Gemini çıktı şeması geçersiz bulundu.";
        }

        return generalMessage;
    }

    private static bool IsInvalidApiKey(
        HttpStatusCode statusCode,
        GeminiErrorDetails? geminiError)
    {
        return statusCode is HttpStatusCode.Unauthorized or
            HttpStatusCode.Forbidden ||
            string.Equals(
                geminiError?.Status,
                "API_KEY_INVALID",
                StringComparison.OrdinalIgnoreCase) ||
            ContainsAny(
                geminiError?.Message,
                "API key not valid",
                "API key is invalid",
                "invalid API key");
    }

    private static bool IsUnavailableModel(
        GeminiErrorDetails? geminiError)
    {
        return ContainsAny(geminiError?.Message, "model", "models/") &&
            (string.Equals(
                geminiError?.Status,
                "NOT_FOUND",
                StringComparison.OrdinalIgnoreCase) ||
             ContainsAny(
                 geminiError?.Message,
                 "not found",
                 "not supported",
                 "unavailable"));
    }

    private static bool ContainsAny(
        string? value,
        params string[] candidates)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            candidates.Any(candidate =>
                value.Contains(
                    candidate,
                    StringComparison.OrdinalIgnoreCase));
    }
}
