using System.IO;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.TacticalMap.Scout;

[Prototype("cmuScoutReportSettings")]
public sealed partial class CMUScoutReportSettingsPrototype : IPrototype, ISerializationHooks
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public int MaxActiveReports;

    [DataField(required: true)]
    public TimeSpan Lifetime;

    [DataField(required: true)]
    public TimeSpan Cooldown;

    [DataField(required: true)]
    public float ReportRange;

    [DataField(required: true)]
    public Dictionary<string, ProtoId<RadioChannelPrototype>[]> RadioChannels = new();

    void ISerializationHooks.AfterDeserialization()
    {
        if (MaxActiveReports <= 0)
            throw new InvalidDataException($"Scout report settings '{ID}': maxActiveReports must be positive.");
        if (Lifetime <= TimeSpan.Zero)
            throw new InvalidDataException($"Scout report settings '{ID}': lifetime must be positive.");
        if (Cooldown < TimeSpan.Zero)
            throw new InvalidDataException($"Scout report settings '{ID}': cooldown cannot be negative.");
        if (!float.IsFinite(ReportRange) || ReportRange <= 0)
            throw new InvalidDataException($"Scout report settings '{ID}': reportRange must be finite and positive.");
    }
}
