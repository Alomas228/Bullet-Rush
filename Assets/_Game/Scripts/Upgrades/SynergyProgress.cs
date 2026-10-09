public class SynergyProgress
{
    public SynergyData Synergy { get; }

    public int Current { get; }

    public int Required { get; }

    public bool IsComplete =>
        Required > 0 && Current >= Required;

    public float Ratio =>
        Required <= 0 ? 0f : (float)Current / Required;

    public SynergyProgress(
        SynergyData synergy,
        RunBuildState build)
    {
        Synergy = synergy;

        if (synergy != null)
        {
            synergy.GetProgress(
                build,
                out int current,
                out int required
            );

            Current = current;
            Required = required;
        }
    }
}
