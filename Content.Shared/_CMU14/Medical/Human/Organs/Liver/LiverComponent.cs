using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Human.Organs.Liver;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedLiverSystem))]
public sealed partial class LiverComponent : Component
{
    [DataField, AutoNetworkedField]
    public float ToxinClearMultiplier = 1.0f;

    [DataField]
    public Dictionary<OrganDamageStatus, FixedPoint2> ToxinPerSecond = new()
    {
        { OrganDamageStatus.None, FixedPoint2.Zero },
        { OrganDamageStatus.LittleBruised, FixedPoint2.Zero },
        { OrganDamageStatus.Bruised, FixedPoint2.Zero },
        { OrganDamageStatus.Broken, FixedPoint2.New(0.5) },
    };

    [DataField, AutoPausedField]
    public TimeSpan NextSelfDamageTick;
}
