using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Text;
using System.Text.Json;
using Vectra.Application.Abstractions.Caches;
using Vectra.Application.Abstractions.Executions;
using Vectra.BuildingBlocks.Configuration.Semantic;
using Vectra.Infrastructure.Caches;

namespace Vectra.Infrastructure.Semantic.Providers.InternalBert;

public class InternalOnnxProvider : ISemanticProvider
{
    private readonly InferenceSession _session;
    private readonly BertTokenizer _tokenizer;
    private readonly ICacheProvider _cacheProvider;
    private readonly ILogger<InternalOnnxProvider> _logger;
    private readonly string[] _intentLabels;
    private readonly int _maxLength;

    public InternalOnnxProvider(
        IOptions<SemanticConfiguration> options, 
        ICacheService cacheService, 
        ILogger<InternalOnnxProvider> logger)
    {
        var modelPath = options.Value.Providers.Internal.ModelPath ?? "/Models/intent_model_onnx/model.onnx";
        var vocabPath = options.Value.Providers.Internal.VocabPath ?? "/Models/intent_model_onnx/vocab.txt";
        var labelsPath = options.Value.Providers.Internal.LabelsPath ?? "/Models/intent_model_onnx/labels.json";
        _maxLength = options.Value.Providers.Internal.MaxLength ?? 128;

        _session = new InferenceSession(modelPath);
        _tokenizer = new BertTokenizer(vocabPath);
        _cacheProvider = cacheService.Current ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var labelsJson = File.ReadAllText(labelsPath);
        var labelDict = JsonSerializer.Deserialize<Dictionary<string, string>>(labelsJson);
        _intentLabels = labelDict!.OrderBy(kv => int.Parse(kv.Key)).Select(kv => kv.Value).ToArray();
    }

    public async Task<SemanticAnalysisResult> AnalyzeAsync(string? requestBody, string metadata, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(requestBody))
            return new SemanticAnalysisResult { Intent = "unknown", Confidence = 0.5, FallbackSafe = true };

        // Cache by exact body
        var cacheKey = $"semantic_internal:{ComputeHash(requestBody)}";
        var (success, cached) = await _cacheProvider.TryGetValueAsync<SemanticAnalysisResult>(cacheKey);
        if (success)
            return cached!;

        // Tokenize
        var (inputIds, attentionMask) = _tokenizer.Tokenize(requestBody, _maxLength);
        var inputTensor = new DenseTensor<long>(inputIds, new[] { 1, _maxLength });
        var maskTensor = new DenseTensor<long>(attentionMask, new[] { 1, _maxLength });

        // Run inference
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", maskTensor)
        };
        using var results = _session.Run(inputs);
        var logits = results.First().AsTensor<float>().ToArray();

        // Softmax
        var probs = Softmax(logits);
        var maxIdx = Array.IndexOf(probs, probs.Max());
        var intent = _intentLabels[maxIdx];
        var confidence = probs[maxIdx];

        var riskTags = intent switch
        {
            "bulk_export" => new[] { "data_exfiltration" },
            "destructive_delete" => new[] { "destructive" },
            "admin_action" => new[] { "privilege_escalation" },
            "harmful" => new[] { "malicious" },
            _ => Array.Empty<string>()
        };

        var result = new SemanticAnalysisResult
        {
            Intent = intent,
            Confidence = confidence,
            RiskTags = riskTags,
            FallbackSafe = confidence < 0.7,
            Explanation = $"Internal ONNX: {intent} ({confidence:F2})"
        };

        await _cacheProvider.SetAsync(cacheKey, result);
        return result;
    }

    private static float[] Softmax(float[] logits)
    {
        var max = logits.Max();
        var exp = logits.Select(x => (float)Math.Exp(x - max)).ToArray();
        var sum = exp.Sum();
        return exp.Select(x => x / sum).ToArray();
    }

    private static string ComputeHash(string input) => Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(input)));
}
