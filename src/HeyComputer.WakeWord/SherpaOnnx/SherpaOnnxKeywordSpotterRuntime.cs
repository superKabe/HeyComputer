using HeyComputer.WakeWord.Models;
using SherpaOnnx;

namespace HeyComputer.WakeWord.SherpaOnnx;

public sealed class SherpaOnnxKeywordSpotterRuntime : IKeywordSpotterRuntime
{
    private readonly object _gate = new();
    private readonly KeywordSpotter _spotter;
    private OnlineStream _stream;
    private bool _disposed;

    private SherpaOnnxKeywordSpotterRuntime(
        KeywordSpotter spotter,
        OnlineStream stream)
    {
        _spotter = spotter;
        _stream = stream;
    }

    public static async Task<SherpaOnnxKeywordSpotterRuntime> CreateAsync(
        string manifestPath,
        SherpaOnnxWakeWordOptions? options = null,
        IReadOnlySet<string>? allowedLicenses = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new SherpaOnnxWakeWordOptions();
        options.Validate();

        var manifest = await WakeWordModelManifest
            .LoadAsync(manifestPath, cancellationToken)
            .ConfigureAwait(false);
        var validation = await WakeWordModelValidator
            .ValidateAsync(manifest, allowedLicenses, cancellationToken)
            .ConfigureAwait(false);
        validation.ThrowIfInvalid();

        var config = new KeywordSpotterConfig();
        config.FeatConfig.SampleRate = 16_000;
        config.FeatConfig.FeatureDim = 80;
        config.ModelConfig.Transducer.Encoder = WakeWordModelValidator.ResolveRequiredFile(manifest, "encoder.onnx");
        config.ModelConfig.Transducer.Decoder = WakeWordModelValidator.ResolveRequiredFile(manifest, "decoder.onnx");
        config.ModelConfig.Transducer.Joiner = WakeWordModelValidator.ResolveRequiredFile(manifest, "joiner.onnx");
        config.ModelConfig.Tokens = WakeWordModelValidator.ResolveRequiredFile(manifest, "tokens.txt");
        config.ModelConfig.Provider = "cpu";
        config.ModelConfig.NumThreads = options.NumThreads;
        config.ModelConfig.Debug = options.Debug ? 1 : 0;
        config.KeywordsBuf = $"{manifest.KeywordTokens} @{StreamingWakeWordDetector.DefaultPhrase}";
        config.KeywordsBufSize = System.Text.Encoding.UTF8.GetByteCount(config.KeywordsBuf);
        config.KeywordsScore = options.KeywordsScore;
        config.KeywordsThreshold = options.KeywordsThreshold;
        config.NumTrailingBlanks = options.NumTrailingBlanks;
        config.MaxActivePaths = options.MaxActivePaths;

        var spotter = new KeywordSpotter(config);
        try
        {
            return new SherpaOnnxKeywordSpotterRuntime(spotter, spotter.CreateStream());
        }
        catch
        {
            spotter.Dispose();
            throw;
        }
    }

    public ValueTask<KeywordDetection?> ProcessAsync(
        ReadOnlyMemory<float> samples,
        int sampleRate,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _stream.AcceptWaveform(sampleRate, samples.ToArray());

            while (_spotter.IsReady(_stream))
            {
                cancellationToken.ThrowIfCancellationRequested();
                _spotter.Decode(_stream);
                var result = _spotter.GetResult(_stream);
                if (!string.IsNullOrWhiteSpace(result.Keyword))
                {
                    var detected = new KeywordDetection(result.Keyword);
                    ResetUnsafe();
                    return ValueTask.FromResult<KeywordDetection?>(detected);
                }
            }

            return ValueTask.FromResult<KeywordDetection?>(null);
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ResetUnsafe();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _stream.Dispose();
            _spotter.Dispose();
        }
    }

    private void ResetUnsafe()
    {
        _spotter.Reset(_stream);
    }
}
