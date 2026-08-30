using Content.Shared.Radio;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Radio;

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
    RadioFrequency tunedFrequency,
    RadioFrequency minFrequency,
    RadioFrequency maxFrequency)
    : BoundUserInterfaceState
{
    public readonly RadioFrequency TunedFrequency = tunedFrequency;
    public readonly RadioFrequency MinFrequency = minFrequency;
    public readonly RadioFrequency MaxFrequency = maxFrequency;
}

public static class TunableFrequencyHelpers
{
    public static string FormatFreq(RadioFrequency frequency) => frequency.FormatMegahertz();
}
