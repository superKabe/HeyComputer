using System.Text.Json;
using System.Text.Json.Serialization;

namespace HeyComputer.WakeWord.Models;

public sealed record WakeWordModelManifest
{
    public required string ModelId { get; init; }

    public required string Version { get; init; }

    public required string SourceUrl { get; init; }

    public required string LicenseSpdx { get; init; }

    public required string LicenseUrl { get; init; }

    public required bool RedistributionAllowed { get; init; }

    public string WakePhrase { get; init; } = StreamingWakeWordDetector.DefaultPhrase;

    /// <summary>The tokenized phrase expected by the selected model vocabulary.</summary>
    public required string KeywordTokens { get; init; }

    public required Dictionary<string, string> Sha256 { get; init; }

    [JsonIgnore]
    public string? BundleDirectory { get; internal set; }

    public static async Task<WakeWordModelManifest> LoadAsync(
        string manifestPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        var fullPath = Path.GetFullPath(manifestPath);
        await using var stream = File.OpenRead(fullPath);
        var manifest = await JsonSerializer.DeserializeAsync<WakeWordModelManifest>(
            stream,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            },
            cancellationToken).ConfigureAwait(false);

        if (manifest is null)
        {
            throw new InvalidDataException("The wake-word model manifest is empty or invalid.");
        }

        manifest.BundleDirectory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidDataException("The model manifest has no parent directory.");
        return manifest;
    }
}
