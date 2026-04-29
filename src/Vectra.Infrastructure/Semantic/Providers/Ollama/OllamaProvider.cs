using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaSharp;
using System.Text;
using Vectra.Application.Abstractions.Caches;
using Vectra.Application.Abstractions.Executions;
using Vectra.BuildingBlocks.Configuration.Semantic;
using Vectra.Infrastructure.Caches;

namespace Vectra.Infrastructure.Semantic.Providers.Ollama;

public class OllamaProvider : SemanticProviderBase, ISemanticProvider
{
    private readonly OllamaApiClient _client;
    private readonly OllamaConfiguration _config;
    private readonly ICacheProvider _cacheProvider;
    private readonly ILogger<OllamaProvider> _logger;



    public OllamaProvider(
        IOptions<SemanticConfiguration> options,
        ICacheService cacheService,
        ILogger<OllamaProvider> logger)
    {
        _config = options.Value.Providers.Ollama;
        _client = new OllamaApiClient(_config.Endpoint, _config.Model);
        _cacheProvider = cacheService.Current ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SemanticAnalysisResult> AnalyzeAsync(string? requestBody, string metadata, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requestBody))
            return new SemanticAnalysisResult { Intent = "unknown", Confidence = 0.5, FallbackSafe = true };

        var cacheKey = $"semantic_ollama:{ComputeHash(requestBody)}";
        var (success, cached) = await _cacheProvider.TryGetValueAsync<SemanticAnalysisResult>(cacheKey);
        if (success)
            return cached!;

        SemanticAnalysisResult result;
        try
        {
            var chat = new Chat(_client, SystemPrompt);
            var userMessage = $"Metadata: {metadata}\n\nRequest body:\n{requestBody}";

            var sb = new StringBuilder();
            await foreach (var token in chat.SendAsync(userMessage, cancellationToken))
                sb.Append(token);

            result = ParseResponse(sb.ToString(), "Ollama");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama semantic provider failed; returning safe fallback");
            result = new SemanticAnalysisResult { Intent = "unknown", Confidence = 0.5, FallbackSafe = true };
        }

        await _cacheProvider.SetAsync(cacheKey, result);
        return result;
    }

    }
