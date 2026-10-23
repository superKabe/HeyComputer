using System.Security.Cryptography;

namespace HeyComputer.WakeWord.Models;

public sealed record WakeWordModelValidation(bool IsValid, IReadOnlyList<string> Errors)
{
    public void ThrowIfInvalid()
    {
        if (!IsValid)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, Errors));
        }
    }
}

public static class WakeWordModelValidator
{
    private static readonly HashSet<string> DefaultRedistributableLicenses = new(
        ["Apache-2.0", "MIT", "BSD-2-Clause", "BSD-3-Clause", "CC0-1.0"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly string[] RequiredFiles =
        ["encoder.onnx", "decoder.onnx", "joiner.onnx", "tokens.txt"];

    public static async Task<WakeWordModelValidation> ValidateAsync(
        WakeWordModelManifest manifest,
        IReadOnlySet<string>? allowedLicenses = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var errors = new List<string>();
        var bundleDirectory = manifest.BundleDirectory;

        if (string.IsNullOrWhiteSpace(bundleDirectory) || !Directory.Exists(bundleDirectory))
        {
            errors.Add("The model bundle directory does not exist.");
            return new(false, errors);
        }

        if (!manifest.RedistributionAllowed)
        {
            errors.Add("The manifest does not affirm that redistribution is allowed.");
        }

        var licenses = allowedLicenses ?? DefaultRedistributableLicenses;
        if (!licenses.Contains(manifest.LicenseSpdx))
        {
            errors.Add($"The model license '{manifest.LicenseSpdx}' is not on the approved redistribution list.");
        }

        ValidateHttpsUrl(manifest.SourceUrl, "source", errors);
        ValidateHttpsUrl(manifest.LicenseUrl, "license", errors);

        if (!string.Equals(
                manifest.WakePhrase.Trim(),
                StreamingWakeWordDetector.DefaultPhrase,
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"The MVP model must target '{StreamingWakeWordDetector.DefaultPhrase}'.");
        }

        if (string.IsNullOrWhiteSpace(manifest.KeywordTokens))
        {
            errors.Add("The model-specific tokenization for the wake phrase is missing.");
        }

        foreach (var fileName in RequiredFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!manifest.Sha256.TryGetValue(fileName, out var expectedHash))
            {
                errors.Add($"The manifest is missing a SHA-256 entry for '{fileName}'.");
                continue;
            }

            if (!TryResolveBundleFile(bundleDirectory, fileName, out var fullPath))
            {
                errors.Add($"The model file path '{fileName}' is not a safe bundle-relative path.");
                continue;
            }

            if (!File.Exists(fullPath))
            {
                errors.Add($"The required model file '{fileName}' is missing.");
                continue;
            }

            if (!IsSha256(expectedHash))
            {
                errors.Add($"The SHA-256 value for '{fileName}' is malformed.");
                continue;
            }

            var actualHash = await ComputeSha256Async(fullPath, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"The SHA-256 value for '{fileName}' does not match the file.");
            }
        }

        return new(errors.Count == 0, errors);
    }

    public static string ResolveRequiredFile(WakeWordModelManifest manifest, string fileName)
    {
        if (string.IsNullOrWhiteSpace(manifest.BundleDirectory) ||
            !RequiredFiles.Contains(fileName, StringComparer.Ordinal) ||
            !TryResolveBundleFile(manifest.BundleDirectory, fileName, out var fullPath))
        {
            throw new InvalidDataException($"'{fileName}' is not a valid required model file.");
        }

        return fullPath;
    }

    private static void ValidateHttpsUrl(string value, string label, ICollection<string> errors)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            errors.Add($"The model {label} URL must be an absolute HTTPS URL.");
        }
    }

    private static bool TryResolveBundleFile(string bundleDirectory, string fileName, out string fullPath)
    {
        var root = Path.GetFullPath(bundleDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        fullPath = Path.GetFullPath(Path.Combine(root, fileName));
        return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(Uri.IsHexDigit);

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }
}
