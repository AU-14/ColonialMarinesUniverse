using Robust.Shared.Serialization;

namespace Content.Shared._AU14.Radio;

[Serializable, NetSerializable]
public enum TunableFrequencyUI
{
    Key,
}

[Serializable, NetSerializable]
public sealed class TunableFrequencySetMsg(string frequencyText) : BoundUserInterfaceMessage
{
    public readonly string FrequencyText = frequencyText;
}

[Serializable, NetSerializable]
public sealed class TunableFrequencyState(
    int tunedFrequency,
    int minFrequency,
    int maxFrequency)
    : BoundUserInterfaceState
{
    public readonly int TunedFrequency = tunedFrequency;
    public readonly int MinFrequency = minFrequency;
    public readonly int MaxFrequency = maxFrequency;
}

public static class TunableFrequencyHelpers
{
    public static string FormatFreq(int raw)
        => raw >= 1000 ? $"{raw / 1000}.{raw % 1000:D3}" : $"00.{raw:D3}";
}
