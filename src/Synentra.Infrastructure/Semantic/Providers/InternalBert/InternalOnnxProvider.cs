using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Synentra.Application.Abstractions.Caches;
using Synentra.Application.Abstractions.Executions;
using Synentra.BuildingBlocks.Configuration.Semantic;
using Synentra.Infrastructure.Caches;
using System.Security.Cryptography;
using System.Text;

namespace Synentra.Infrastructure.Semantic.Providers.InternalBert;

/// <summary>
/// ONNX-based semantic provider.
///
/// Reads the Community or Pro model package from the local file system and
/// loads all required assets into memory. No temporary files or network
/// access are used during inference.
/// </summary>
public sealed class InternalOnnxProvider : ISemanticProvider, IDisposable
{
    private const string FallbackIntent = "suspicious";
    private const double DefaultFallbackConfidence = 0.5;
    private const double DefaultConfidenceThreshold = 0.7;

    private readonly ICacheProvider? _cacheProvider;
    private readonly IModelPackageLoader _loader;
    private readonly ILogger<InternalOnnxProvider> _logger;
    private readonly InternalOnnxConfiguration? _internalConfig;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);

    private readonly TaskCompletionSource _initializationTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly int _maxLength;
    private readonly double _confidenceThreshold;
    private readonly bool _enabled;

    private InferenceSession? _session;
    private BertTokenizer? _tokenizer;
    private string[] _intentLabels = [];

    private bool _disposed;

    public InternalOnnxProvider(
        IOptions<SemanticConfiguration> options,
        ICacheService cacheService,
        IModelPackageLoader loader,
        ILogger<InternalOnnxProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(cacheService);

        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var semanticConfig = options.Value
            ?? throw new InvalidOperationException(
                "Semantic configuration is unavailable.");

        _enabled = semanticConfig.Enabled ?? false;

        if (!_enabled)
        {
            _maxLength = 128;
            _confidenceThreshold = DefaultConfidenceThreshold;

            _logger.LogInformation(
                "Semantic analysis is disabled. Internal ONNX model loading will be skipped.");

            _initializationTcs.TrySetResult();
            return;
        }

        _internalConfig = semanticConfig.Providers?.Internal
            ?? throw new InvalidOperationException(
                "Internal ONNX configuration is missing.");

        _maxLength = _internalConfig.ModelSize ?? 128;

        if (_maxLength <= 0)
        {
            throw new InvalidOperationException(
                $"Internal ONNX model size must be greater than zero. " +
                $"Configured value: {_maxLength}.");
        }

        // Adjust this property path if ConfidenceThreshold belongs to a
        // different configuration object in your solution.
        _confidenceThreshold =
            semanticConfig.ConfidenceThreshold ?? DefaultConfidenceThreshold;

        if (_confidenceThreshold is < 0 or > 1)
        {
            throw new InvalidOperationException(
                $"Semantic confidence threshold must be between 0 and 1. " +
                $"Configured value: {_confidenceThreshold}.");
        }

        _cacheProvider = cacheService.Current
            ?? throw new InvalidOperationException(
                "No cache provider is currently configured.");
    }

    /// <summary>
    /// Loads the ONNX model, tokenizer, and intent labels into memory.
    /// Safe to call multiple times or concurrently.
    /// </summary>
    public async Task InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!_enabled)
            return;

        if (_initializationTcs.Task.IsCompleted)
        {
            await _initializationTcs.Task.WaitAsync(cancellationToken);
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken);

        try
        {
            // Another caller may have completed initialization while this
            // caller was waiting for the lock.
            if (_initializationTcs.Task.IsCompleted)
            {
                await _initializationTcs.Task.WaitAsync(cancellationToken);
                return;
            }

            var config = _internalConfig
                ?? throw new InvalidOperationException(
                    "Internal ONNX configuration is unavailable.");

            _logger.LogInformation(
                "Loading Internal ONNX model with maximum sequence length {MaxLength}.",
                _maxLength);

            var assets = await _loader.LoadAsync(
                config,
                cancellationToken);

            if (assets.OnnxBytes.IsEmpty)
            {
                throw new InvalidOperationException(
                    "The model package contains an empty ONNX model.");
            }

            if (assets.VocabLines is null || !assets.VocabLines.Any())
            {
                throw new InvalidOperationException(
                    "The model package does not contain a valid tokenizer vocabulary.");
            }

            if (assets.IntentLabels is null || assets.IntentLabels.Length == 0)
            {
                throw new InvalidOperationException(
                    "The model package does not contain intent labels.");
            }

            var sessionOptions = new SessionOptions();

            var session = new InferenceSession(
                assets.OnnxBytes.ToArray(),
                sessionOptions);

            var tokenizer = new BertTokenizer(assets.VocabLines);
            var labels = assets.IntentLabels;

            ValidateModelMetadata(session, labels);

            _session = session;
            _tokenizer = tokenizer;
            _intentLabels = labels;

            _logger.LogInformation(
                "Internal ONNX model loaded successfully with {LabelCount} labels. " +
                "Inputs: {Inputs}; Outputs: {Outputs}.",
                labels.Length,
                FormatInputMetadata(session.InputMetadata),
                string.Join(", ", session.OutputMetadata.Keys));

            _initializationTcs.TrySetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to initialize the Internal ONNX semantic provider.");

            _initializationTcs.TrySetException(ex);

            // Initialization failure should propagate so the application
            // does not silently run with a configured but unavailable model.
            throw;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public async Task<SemanticAnalysisResult> AnalyzeAsync(
        string? semanticInput,
        string metadata,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (!_enabled)
            return CreateFallbackResult("Semantic analysis is disabled.");

        await _initializationTcs.Task.WaitAsync(cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(semanticInput))
        {
            return CreateFallbackResult(
                "Semantic input is empty.");
        }

        var cacheKey = $"semantic_internal:{ComputeHash(semanticInput)}";

        var (cacheHit, cachedResult) =
            await _cacheProvider!.TryGetValueAsync<SemanticAnalysisResult>(
                cacheKey);

        if (cacheHit && cachedResult is not null)
        {
            _logger.LogDebug(
                "Internal ONNX semantic result loaded from cache.");

            return cachedResult;
        }

        var session = _session
            ?? throw new InvalidOperationException(
                "Internal ONNX session is not initialized.");

        var tokenizer = _tokenizer
            ?? throw new InvalidOperationException(
                "Internal ONNX tokenizer is not initialized.");

        var (rawIds, rawMask) = tokenizer.Tokenize(
            semanticInput,
            _maxLength);

        if (rawIds is null)
        {
            throw new InvalidOperationException(
                "The tokenizer returned null input IDs.");
        }

        if (rawMask is null)
        {
            throw new InvalidOperationException(
                "The tokenizer returned a null attention mask.");
        }

        _logger.LogDebug(
            "Tokenized semantic input. Input IDs: {InputIdCount}; " +
            "attention mask: {AttentionMaskCount}.",
            rawIds.Length,
            rawMask.Length);

        var inputIds = PadOrTruncate(rawIds, _maxLength);
        var attentionMask = PadOrTruncate(rawMask, _maxLength);
        var tokenTypeIds = new long[_maxLength];

        var availableInputs =
            new Dictionary<string, DenseTensor<long>>(
                StringComparer.Ordinal)
            {
                ["input_ids"] = new(
                    inputIds,
                    new[] { 1, _maxLength }),

                ["attention_mask"] = new(
                    attentionMask,
                    new[] { 1, _maxLength }),

                ["token_type_ids"] = new(
                    tokenTypeIds,
                    new[] { 1, _maxLength })
            };

        var inputs = CreateModelInputs(
            session,
            availableInputs);

        _logger.LogDebug(
            "Running Internal ONNX inference with inputs: {Inputs}.",
            string.Join(", ", inputs.Select(input => input.Name)));

        try
        {
            using var results = session.Run(inputs);

            var output = results.FirstOrDefault()
                ?? throw new InvalidOperationException(
                    "The ONNX model returned no outputs.");

            var logits = output.AsTensor<float>().ToArray();

            ValidateLogits(logits);

            var probabilities = Softmax(logits);
            var maximumProbability = probabilities.Max();
            var maximumIndex = Array.IndexOf(
                probabilities,
                maximumProbability);

            if (maximumIndex < 0 ||
                maximumIndex >= _intentLabels.Length)
            {
                throw new InvalidOperationException(
                    $"The predicted label index {maximumIndex} is outside " +
                    $"the labels range 0-{_intentLabels.Length - 1}.");
            }

            var intent = _intentLabels[maximumIndex];
            var confidence = probabilities[maximumIndex];

            var result = new SemanticAnalysisResult
            {
                Intent = intent,
                Confidence = confidence,
                RiskTags = GetRiskTags(intent),
                FallbackSafe = confidence < _confidenceThreshold,
                Explanation =
                    $"Internal ONNX: {intent} ({confidence:F2})"
            };

            await _cacheProvider.SetAsync(cacheKey, result);

            _logger.LogDebug(
                "Internal ONNX inference completed. Intent: {Intent}; " +
                "confidence: {Confidence:F4}; fallback safe: {FallbackSafe}.",
                result.Intent,
                result.Confidence,
                result.FallbackSafe);

            return result;
        }
        catch (OnnxRuntimeException ex)
        {
            _logger.LogError(
                ex,
                "ONNX Runtime failed during semantic inference. " +
                "Model inputs: {ModelInputs}; supplied inputs: {SuppliedInputs}.",
                FormatInputMetadata(session.InputMetadata),
                string.Join(", ", inputs.Select(input => input.Name)));

            throw;
        }
        catch (NullReferenceException ex)
        {
            // Some ONNX Runtime input/native-runtime mismatches can surface
            // as NullReferenceException from RunImpl.
            _logger.LogError(
                ex,
                "ONNX Runtime encountered an invalid internal value during inference. " +
                "Verify model input names, tensor types, dimensions, and ONNX Runtime " +
                "managed/native package versions. Model inputs: {ModelInputs}; " +
                "supplied inputs: {SuppliedInputs}.",
                FormatInputMetadata(session.InputMetadata),
                string.Join(", ", inputs.Select(input => input.Name)));

            throw new InvalidOperationException(
                "Internal ONNX inference failed. Verify the model input metadata " +
                "and ensure all Microsoft.ML.OnnxRuntime packages use the same version.",
                ex);
        }
    }

    private IReadOnlyCollection<NamedOnnxValue> CreateModelInputs(
        InferenceSession session,
        IReadOnlyDictionary<string, DenseTensor<long>> availableInputs)
    {
        var modelMetadata = session.InputMetadata;

        if (modelMetadata.Count == 0)
        {
            throw new InvalidOperationException(
                "The ONNX model declares no inputs.");
        }

        var inputs = new List<NamedOnnxValue>(
            modelMetadata.Count);

        foreach (var (inputName, inputMetadata) in modelMetadata)
        {
            if (!availableInputs.TryGetValue(
                    inputName,
                    out var tensor))
            {
                throw new InvalidOperationException(
                    $"The ONNX model requires input '{inputName}', but the " +
                    $"provider cannot create it. Model inputs: " +
                    $"{string.Join(", ", modelMetadata.Keys)}.");
            }

            if (inputMetadata.ElementType != typeof(long))
            {
                throw new InvalidOperationException(
                    $"ONNX input '{inputName}' expects element type " +
                    $"'{inputMetadata.ElementType}', but the provider supplies " +
                    $"'System.Int64'.");
            }

            ValidateInputDimensions(
                inputName,
                inputMetadata.Dimensions,
                tensor.Dimensions);

            var input = NamedOnnxValue.CreateFromTensor(
                inputName,
                tensor);

            if (input is null)
            {
                throw new InvalidOperationException(
                    $"Failed to create ONNX value for input '{inputName}'.");
            }

            inputs.Add(input);
        }

        if (inputs.Count != modelMetadata.Count)
        {
            throw new InvalidOperationException(
                $"ONNX model input mismatch. Expected {modelMetadata.Count} " +
                $"inputs but created {inputs.Count}. Expected inputs: " +
                $"{string.Join(", ", modelMetadata.Keys)}. Created inputs: " +
                $"{string.Join(", ", inputs.Select(input => input.Name))}.");
        }

        return inputs;
    }

    private static void ValidateInputDimensions(
        string inputName,
        IReadOnlyList<int> expectedDimensions,
        ReadOnlySpan<int> actualDimensions)
    {
        if (expectedDimensions.Count != actualDimensions.Length)
        {
            throw new InvalidOperationException(
                $"ONNX input '{inputName}' expects {expectedDimensions.Count} " +
                $"dimensions but received {actualDimensions.Length}. Expected: " +
                $"[{string.Join(",", expectedDimensions)}]; actual: " +
                $"[{string.Join(",", actualDimensions.ToArray())}].");
        }

        for (var index = 0;
             index < expectedDimensions.Count;
             index++)
        {
            var expected = expectedDimensions[index];
            var actual = actualDimensions[index];

            // Negative dimensions represent dynamic dimensions.
            if (expected >= 0 && expected != actual)
            {
                throw new InvalidOperationException(
                    $"ONNX input '{inputName}' has an invalid dimension at " +
                    $"index {index}. Expected: " +
                    $"[{string.Join(",", expectedDimensions)}]; actual: " +
                    $"[{string.Join(",", actualDimensions.ToArray())}].");
            }
        }
    }

    private static void ValidateModelMetadata(
        InferenceSession session,
        IReadOnlyCollection<string> labels)
    {
        if (session.InputMetadata.Count == 0)
        {
            throw new InvalidOperationException(
                "The ONNX model does not declare any inputs.");
        }

        if (!session.InputMetadata.ContainsKey("input_ids"))
        {
            throw new InvalidOperationException(
                "The ONNX model does not declare the required " +
                "'input_ids' input.");
        }

        if (!session.InputMetadata.ContainsKey("attention_mask"))
        {
            throw new InvalidOperationException(
                "The ONNX model does not declare the required " +
                "'attention_mask' input.");
        }

        if (session.OutputMetadata.Count == 0)
        {
            throw new InvalidOperationException(
                "The ONNX model does not declare any outputs.");
        }

        if (labels.Count == 0)
        {
            throw new InvalidOperationException(
                "No intent labels were loaded.");
        }
    }

    private void ValidateLogits(float[] logits)
    {
        if (logits.Length == 0)
        {
            throw new InvalidOperationException(
                "The ONNX model returned an empty logits tensor.");
        }

        if (logits.Length != _intentLabels.Length)
        {
            throw new InvalidOperationException(
                $"The ONNX model returned {logits.Length} logits, but " +
                $"{_intentLabels.Length} intent labels were loaded.");
        }

        if (logits.Any(value =>
                float.IsNaN(value) ||
                float.IsInfinity(value)))
        {
            throw new InvalidOperationException(
                "The ONNX model returned non-finite logits.");
        }
    }

    private static float[] Softmax(float[] logits)
    {
        var maximum = logits.Max();

        var exponentials = logits
            .Select(value =>
                (float)Math.Exp(value - maximum))
            .ToArray();

        var sum = exponentials.Sum();

        if (sum <= 0 ||
            float.IsNaN(sum) ||
            float.IsInfinity(sum))
        {
            throw new InvalidOperationException(
                "Unable to calculate probabilities from the ONNX logits.");
        }

        return exponentials
            .Select(value => value / sum)
            .ToArray();
    }

    private static string[] GetRiskTags(string intent)
    {
        return intent switch
        {
            "bulk_export" =>
                ["data_exfiltration"],

            "export" =>
                ["data_exfiltration"],

            "destructive_delete" =>
                ["destructive"],

            "soft_delete" =>
                ["destructive"],

            "admin_action" =>
                ["privilege_escalation"],

            "escalate_privileges" =>
                ["privilege_escalation"],

            "harmful" =>
                ["malicious"],

            "suspicious" =>
                ["malicious"],

            _ => []
        };
    }

    private static string ComputeHash(string input)
    {
        var hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(input));

        return Convert.ToHexString(hash);
    }

    private static long[] PadOrTruncate(
        long[] values,
        int requiredLength)
    {
        if (values.Length == requiredLength)
            return values;

        if (values.Length > requiredLength)
            return values[..requiredLength];

        var paddedValues = new long[requiredLength];

        Array.Copy(
            values,
            paddedValues,
            values.Length);

        return paddedValues;
    }

    private static string FormatInputMetadata(
        IReadOnlyDictionary<string, NodeMetadata> metadata)
    {
        return string.Join(
            ", ",
            metadata.Select(entry =>
                $"{entry.Key}:{entry.Value.ElementType}" +
                $"[{string.Join(",", entry.Value.Dimensions)}]"));
    }

    private static SemanticAnalysisResult CreateFallbackResult(
        string explanation)
    {
        return new SemanticAnalysisResult
        {
            Intent = FallbackIntent,
            Confidence = DefaultFallbackConfidence,
            RiskTags = ["malicious"],
            FallbackSafe = true,
            Explanation = explanation
        };
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _session?.Dispose();
        _session = null;
        _tokenizer = null;
        _intentLabels = [];

        _initializationLock.Dispose();
    }
}