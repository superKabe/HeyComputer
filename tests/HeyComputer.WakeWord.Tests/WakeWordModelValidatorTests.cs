using System.Security.Cryptography;
using System.Text.Json;
using HeyComputer.WakeWord.Models;

namespace HeyComputer.WakeWord.Tests;

public sealed class WakeWordModelValidatorTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"hey-computer-kws-{Guid.NewGuid():N}");

    public WakeWordModelValidatorTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task AcceptsCompleteRedistributableBundleWithMatchingHashes()
    {
        var manifest = await CreateManifestAsync();

        var result = await WakeWordModelValidator.ValidateAsync(manifest);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public async Task RejectsNonRedistributableModel()
    {
        var manifest = (await CreateManifestAsync()) with { RedistributionAllowed = false };

        var result = await WakeWordModelValidator.ValidateAsync(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("redistribution", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RejectsHashMismatch()
    {
        var manifest = await CreateManifestAsync();
        await File.WriteAllTextAsync(Path.Combine(_directory, "encoder.onnx"), "changed");

        var result = await WakeWordModelValidator.ValidateAsync(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("does not match", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RejectsUnapprovedLicense()
    {
        var manifest = (await CreateManifestAsync()) with { LicenseSpdx = "LicenseRef-Unknown" };

        var result = await WakeWordModelValidator.ValidateAsync(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("license", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private async Task<WakeWordModelManifest> CreateManifestAsync()
    {
        var files = new[] { "encoder.onnx", "decoder.onnx", "joiner.onnx", "tokens.txt" };
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(file);
            await File.WriteAllBytesAsync(Path.Combine(_directory, file), bytes);
            hashes[file] = Convert.ToHexString(SHA256.HashData(bytes));
        }

        var manifest = new WakeWordModelManifest
        {
            ModelId = "test-model",
            Version = "1",
            SourceUrl = "https://example.com/model",
            LicenseSpdx = "Apache-2.0",
            LicenseUrl = "https://example.com/license",
            RedistributionAllowed = true,
            KeywordTokens = "▁HEY ▁COMPUTER",
            Sha256 = hashes
        };

        var manifestPath = Path.Combine(_directory, "manifest.json");
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest));
        return await WakeWordModelManifest.LoadAsync(manifestPath);
    }
}
