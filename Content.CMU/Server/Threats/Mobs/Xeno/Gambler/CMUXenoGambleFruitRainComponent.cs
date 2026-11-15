using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.CMU14.Threats.Mobs.Xeno.Gambler;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CMUXenoGambleFruitRainComponent : Component
{
    [DataField]
    public List<EntProtoId> Fruits = new();

    [DataField]
    public int Remaining;

    [DataField]
    public float Radius;

    [DataField]
    public TimeSpan Interval;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextDrop;

    [DataField]
    public SoundSpecifier? DropSound;
}
