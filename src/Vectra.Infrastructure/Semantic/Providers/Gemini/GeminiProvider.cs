using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mscc.GenerativeAI;
using Mscc.GenerativeAI.Types;
using Vectra.Application.Abstractions.Caches;
using Vectra.Application.Abstractions.Executions;
using Vectra.BuildingBlocks.Configuration.Semantic;
using Vectra.Infrastructure.Caches;

namespace Vectra.Infrastructure.Semantic.Providers.Gemini;

public class GeminiProvider : SemanticProviderBase, ISemanticProvider
{
    private readonly GenerativeModel _model;
    private readonly ICacheProvider _cacheProvider;
    private readonly ILogger<GeminiProvider> _logger;



    public GeminiProvider(
        IOptions<SemanticConfiguration> options,
        ICacheService cacheService,
        ILogger<GeminiProvider> logger)
    {
        var config = options.Value.Providers.Gemini;
        var googleAi = new GoogleAI(apiKey: config.ApiKey);
        _model = googleAi.GenerativeModel(
            model: config.Model,
            generationConfig: new GenerationConfig
            {
                Temperature = (float?)config.Temperature,
                MaxOutputTokens = config.MaxTokens
            });
        _cacheProvider = cacheService.Current ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SemanticAnalysisResult> AnalyzeAsync(string? requestBody, string metadata, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requestBody))
            return new SemanticAnalysisResult { Intent = "unknown", Confidence = 0.5, FallbackSafe = true };

        var cacheKey = $"semantic_gemini:{ComputeHash(requestBody)}";
        var (success, cached) = await _cacheProvider.TryGetValueAsync<SemanticAnalysisResult>(cacheKey);
        if (success)
            return cached!;

        var prompt = $"{SystemPrompt}\n\nMetadata: {metadata}\n\nRequest body:\n{requestBody}";

        SemanticAnalysisResult result;
        try
        {
            var response = await _model.GenerateContent(prompt, cancellationToken: cancellationToken);
            result = ParseResponse(response.Text ?? string.Empty, "Gemini");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini semantic provider failed; returning safe fallback");
            result = new SemanticAnalysisResult { Intent = "unknown", Confidence = 0.5, FallbackSafe = true };
        }

        await _cacheProvider.SetAsync(cacheKey, result);
        return result;
    }

    }
