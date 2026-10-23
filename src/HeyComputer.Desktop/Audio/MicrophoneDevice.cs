namespace HeyComputer.Desktop.Audio;

public sealed record MicrophoneDevice(string? Id, string Name)
{
    public bool IsSystemDefault => Id is null;
}
