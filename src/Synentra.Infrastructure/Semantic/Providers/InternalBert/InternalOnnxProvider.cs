using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Text;
using Synentra.Application.Abstractions.Caches;
using Synentra.Application.Abstractions.Executions;
using Synentra.BuildingBlocks.Configuration.Semantic;
using Synentra.Infrastructure.Caches;

namespace Synentra.Infrastructure.Semantic.Providers.InternalBert;

/// <summary>
/// ONNX-based semantic provider. Reads the model ZIP (Community or Pro) from the local
/// file system and loads all assets entirely in memory — no network, no temp files.
/// </summary>
public sealed class InternalOnnxProvider : ISemanticProvider, IDisposable
{
    private InferenceSession? _session;
    private BertTokenizer? _tokenizer;
    private string[] _intentLabels = [];
    private readonly int _maxLength;
    private readonly ICacheProvider? _cacheProvider;
    private readonly IModelPackageLoader _loader;
    private readonly ILogger<InternalOnnxProvider> _logger;
    private bool _enabled;
    private InternalOnnxConfiguration? _internalConfig;
    private readonly TaskCompletionSource _initializationTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public InternalOnnxProvider(
        IOptions<SemanticConfiguration> options,
        ICacheService cacheService,
        IModelPackageLoader loader,
        ILogger<InternalOnnxProvider> logger)
    {
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _logger  = logger ?? throw new ArgumentNullException(nameof(logger));

        var config = options.Value;
        _enabled = config.Enabled ?? false;

        if (!_enabled)
        {
            _logger.LogInformation("Semantic is disabled — skipping Internal ONNX model loading.");
            _initializationTcs.SetResult(); // marked as "ready" (but disabled)
            return;
        }

        _internalConfig = config.Providers.Internal;
        _maxLength     = _internalConfig.MaxLength ?? 128;
        _cacheProvider = cacheService.Current ?? throw new ArgumentNullException(nameof(cacheService));
    }

    /// <summary>
    /// Must be called once before using the provider. Safe to call multiple times.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!_enabled || _initializationTcs.Task.IsCompleted)
            return;

        try
        {
            var options = Microsoft.Extensions.Options.Options.Create(
                // Re‑fetch configuration; we can also store the InternalOnnxConfiguration in the ctor
                // For simplicity, we'll inject IOptions<SemanticConfiguration> again or store it.
                // Here we'll use a stored field.
                // For clean code, we'll capture the config in a field.
                _internalConfig ?? throw new InvalidOperationException());

            var assets = await _loader.LoadAsync(_internalConfig, cancellationToken);
            _session = new InferenceSession(assets.OnnxBytes.ToArray(), new SessionOptions());
            _tokenizer = new BertTokenizer(assets.VocabLines);
            _intentLabels = assets.IntentLabels;
            _logger.LogInformation("Internal ONNX model loaded successfully.");
            _initializationTcs.SetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Internal ONNX provider. The provider will remain disabled.");
            _initializationTcs.SetException(ex);
            // Disable to avoid throwing on every AnalyzeAsync call
            _enabled = false;
        }
    }

    public async Task<SemanticAnalysisResult> AnalyzeAsync(
        string? body,
        string metadata,
        CancellationToken cancellationToken)
    {
        if (!_enabled)
            return new SemanticAnalysisResult { Intent = "suspicious", Confidence = 0.5, FallbackSafe = true };

        // Wait for initialization to complete (or throw if it failed)
        await _initializationTcs.Task.WaitAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(body))
            return new SemanticAnalysisResult { Intent = "suspicious", Confidence = 0.5, FallbackSafe = true };

        var cacheKey = $"semantic_internal:{ComputeHash(body)}";
        var (success, cached) = await _cacheProvider!.TryGetValueAsync<SemanticAnalysisResult>(cacheKey);
        if (success)
            return cached!;

        var (inputIds, attentionMask) = _tokenizer!.Tokenize(body, _maxLength);
        var inputTensor = new DenseTensor<long>(inputIds, new[] { 1, _maxLength });
        var maskTensor  = new DenseTensor<long>(attentionMask, new[] { 1, _maxLength });

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids",      inputTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", maskTensor)
        };

        using var results = _session!.Run(inputs);
        var logits     = results.First().AsTensor<float>().ToArray();
        var probs      = Softmax(logits);
        var maxIdx     = Array.IndexOf(probs, probs.Max());
        var intent     = _intentLabels[maxIdx];
        var confidence = probs[maxIdx];

        var riskTags = intent switch
        {
            "bulk_export"        => new[] { "data_exfiltration" },
            "export"             => new[] { "data_exfiltration" },
            "destructive_delete" => new[] { "destructive" },
            "soft_delete"        => new[] { "destructive" },
            "admin_action"       => new[] { "privilege_escalation" },
            "escalate_privileges" => new[] { "privilege_escalation" },
            "harmful"            => new[] { "malicious" },
            "suspicious"         => new[] { "malicious" },
            _                    => Array.Empty<string>()
        };

        var result = new SemanticAnalysisResult
        {
            Intent       = intent,
            Confidence   = confidence,
            RiskTags     = riskTags,
            FallbackSafe = confidence < 0.7,
            Explanation  = $"Internal ONNX: {intent} ({confidence:F2})"
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

    private static string ComputeHash(string input) =>
        Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(input)));

    public void Dispose() => _session?.Dispose();
}