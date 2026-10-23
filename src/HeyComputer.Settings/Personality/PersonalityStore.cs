using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using HeyComputer.Core.Personality;

namespace HeyComputer.Settings.Personality;

public sealed class PersonalityStore
{
    internal const int MaximumFileBytes = 128 * 1024;
    internal const int MaximumPromptLength = 32_000;
    internal const int MaximumCustomizationLength = 8_000;
    internal const int MaximumTraitLength = 500;
    internal const int MaximumEasterEggPolicyLength = 4_000;

    private const string EmbeddedDefaultName =
        "HeyComputer.Settings.Defaults.default-personality.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _overridePath;

    public PersonalityStore(string overridePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(overridePath);
        _overridePath = Path.GetFullPath(overridePath);
    }

    public string OverridePath => _overridePath;

    public async Task<PersonalityProfile> LoadAsync(CancellationToken cancellationToken = default)
    {
        return (await LoadRuntimeAsync(cancellationToken).ConfigureAwait(false)).Profile;
    }

    public async Task<RuntimePersonality> LoadRuntimeAsync(
        CancellationToken cancellationToken = default)
    {
        var defaultRuntime = await LoadRuntimeDefaultAsync(cancellationToken).ConfigureAwait(false);
        var baseline = defaultRuntime.Profile;
        if (!File.Exists(_overridePath))
        {
            return defaultRuntime;
        }

        var userOverride = await ReadJsonFileAsync<PersonalityOverride>(
                _overridePath,
                cancellationToken)
            .ConfigureAwait(false);

        return defaultRuntime with { Profile = userOverride.ApplyTo(baseline) };
    }

    public async Task<PersonalityProfile> SaveOverrideAsync(
        PersonalityOverride userOverride,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userOverride);
        userOverride.Validate();

        var baseline = await LoadDefaultAsync(cancellationToken).ConfigureAwait(false);
        var resolved = userOverride.ApplyTo(baseline);
        var directory = Path.GetDirectoryName(_overridePath)
            ?? throw new InvalidOperationException("The override path has no parent directory.");
        Directory.CreateDirectory(directory);

        var temporaryPath = _overridePath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await using (var stream = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             4096,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, userOverride, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, _overridePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }

        return resolved;
    }

    public Task ResetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (File.Exists(_overridePath))
        {
            File.Delete(_overridePath);
        }

        return Task.CompletedTask;
    }

    public static async Task<PersonalityProfile> LoadDefaultAsync(
        CancellationToken cancellationToken = default)
    {
        return (await LoadRuntimeDefaultAsync(cancellationToken).ConfigureAwait(false)).Profile;
    }

    public static async Task<RuntimePersonality> LoadRuntimeDefaultAsync(
        CancellationToken cancellationToken = default)
    {
        await using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(EmbeddedDefaultName)
            ?? throw new InvalidOperationException("The embedded default personality is missing.");

        var document = await DeserializeAsync<PersonalityDocument>(stream, cancellationToken)
            .ConfigureAwait(false);
        document.Validate();
        return new RuntimePersonality(document.Profile, document.EasterEggs);
    }

    public static async Task<PersonalityProfile> LoadDefaultFileAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var document = await ReadJsonFileAsync<PersonalityDocument>(path, cancellationToken)
            .ConfigureAwait(false);
        document.Validate();
        return document.Profile;
    }

    private static async Task<T> ReadJsonFileAsync<T>(
        string path,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var info = new FileInfo(Path.GetFullPath(path));
        if (!info.Exists)
        {
            throw new FileNotFoundException("The personality settings file was not found.", info.FullName);
        }

        if (info.Length > MaximumFileBytes)
        {
            throw new InvalidDataException("The personality settings file is too large.");
        }

        await using var stream = new FileStream(
            info.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await DeserializeAsync<T>(stream, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<T> DeserializeAsync<T>(
        Stream stream,
        CancellationToken cancellationToken)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken)
                       .ConfigureAwait(false)
                   ?? throw new InvalidDataException("The personality settings file is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The personality settings file is invalid.", exception);
        }
    }
}
