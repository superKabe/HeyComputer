namespace HeyComputer.ComputerControl;

public enum LaunchTargetKind
{
    Executable,
    Uri
}

public sealed record LaunchTarget(
    string Id,
    string DisplayName,
    LaunchTargetKind Kind,
    string Value);

public sealed class LaunchAllowlist
{
    private readonly IReadOnlyDictionary<string, LaunchTarget> _targets;

    public LaunchAllowlist(IEnumerable<LaunchTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);

        var validated = targets.Select(Validate).ToArray();
        _targets = validated.ToDictionary(target => target.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<LaunchTarget> Targets => _targets.Values.ToArray();

    public bool TryGet(string targetId, out LaunchTarget? target)
    {
        if (string.IsNullOrWhiteSpace(targetId))
        {
            target = null;
            return false;
        }

        return _targets.TryGetValue(targetId.Trim(), out target);
    }

    private static LaunchTarget Validate(LaunchTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (string.IsNullOrWhiteSpace(target.Id))
        {
            throw new ArgumentException("A launch target must have an ID.", nameof(target));
        }

        if (string.IsNullOrWhiteSpace(target.DisplayName))
        {
            throw new ArgumentException("A launch target must have a display name.", nameof(target));
        }

        if (string.IsNullOrWhiteSpace(target.Value))
        {
            throw new ArgumentException("A launch target must have a value.", nameof(target));
        }

        if (target.Kind == LaunchTargetKind.Uri)
        {
            if (!Uri.TryCreate(target.Value, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != "spotify"))
            {
                throw new ArgumentException(
                    "Allowlisted URIs must use http, https, or spotify.",
                    nameof(target));
            }
        }

        return target with
        {
            Id = target.Id.Trim(),
            DisplayName = target.DisplayName.Trim(),
            Value = target.Value.Trim()
        };
    }
}
