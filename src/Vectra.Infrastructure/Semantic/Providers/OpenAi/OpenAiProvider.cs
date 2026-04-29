using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using Vectra.Application.Abstractions.Caches;
using Vectra.Application.Abstractions.Executions;
using Vectra.BuildingBlocks.Configuration.Semantic;
using Vectra.Infrastructure.Caches;

namespace Vectra.Infrastructure.Semantic.Providers.OpenAi;

public class OpenAiProvider : SemanticProviderBase, ISemanticProvider
{
    private readonly ChatClient _chatClient;
    private readonly OpenAiConfiguration _config;
    private readonly ICacheProvider _cacheProvider;
    private readonly ILogger<OpenAiProvider> _logger;



    public OpenAiProvider(
        IOptions<SemanticConfiguration> options,
        ICacheService cacheService,
        ILogger<OpenAiProvider> logger)
    {
        _config = options.Value.Providers.OpenAi;
        var openAiClient = new OpenAIClient(_config.ApiKey);
        _chatClient = openAiClient.GetChatClient(_config.Model);
        _cacheProvider = cacheService.Current ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SemanticAnalysisResult> AnalyzeAsync(string? requestBody, string metadata, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requestBody))
            return new SemanticAnalysisResult { Intent = "unknown", Confidence = 0.5, FallbackSafe = true };

        var cacheKey = $"semantic_openai:{ComputeHash(requestBody)}";
        var (success, cached) = await _cacheProvider.TryGetValueAsync<SemanticAnalysisResult>(cacheKey);
        if (success)
            return cached!;

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(SystemPrompt),
            new UserChatMessage($"Metadata: {metadata}\n\nRequest body:\n{requestBody}")
        };

        var requestOptions = new ChatCompletionOptions
        {
            Temperature = (float?)_config.Temperature,
            MaxOutputTokenCount = _config.MaxTokens
        };

        SemanticAnalysisResult result;
        try
        {
            var response = await _chatClient.CompleteChatAsync(messages, requestOptions, cancellationToken);
            var content = response.Value.Content[0].Text;
            result = ParseResponse(content, "OpenAI");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI semantic provider failed; returning safe fallback");
            result = new SemanticAnalysisResult { Intent = "unknown", Confidence = 0.5, FallbackSafe = true };
        }

        await _cacheProvider.SetAsync(cacheKey, result);
        return result;
    }

    }
