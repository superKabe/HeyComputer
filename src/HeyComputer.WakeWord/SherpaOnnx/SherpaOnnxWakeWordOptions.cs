namespace HeyComputer.WakeWord.SherpaOnnx;

public sealed record SherpaOnnxWakeWordOptions(
    float KeywordsScore = 1.5f,
    float KeywordsThreshold = 0.25f,
    int NumTrailingBlanks = 2,
    int MaxActivePaths = 4,
    int NumThreads = 1,
    bool Debug = false)
{
    public void Validate()
    {
        if (KeywordsScore <= 0 || float.IsNaN(KeywordsScore))
        {
            throw new ArgumentOutOfRangeException(nameof(KeywordsScore));
        }

        if (KeywordsThreshold is <= 0 or > 1 || float.IsNaN(KeywordsThreshold))
        {
            throw new ArgumentOutOfRangeException(nameof(KeywordsThreshold));
        }

        if (NumTrailingBlanks < 0 || MaxActivePaths <= 0 || NumThreads <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(NumThreads), "Decoder counts must be positive.");
        }
    }
}
