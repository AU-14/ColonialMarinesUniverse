using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Threats.Mobs.Xeno.Gambler;

[RegisterComponent]
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

    [DataField]
    public TimeSpan NextDrop;

    [DataField]
    public SoundSpecifier? DropSound;
}
