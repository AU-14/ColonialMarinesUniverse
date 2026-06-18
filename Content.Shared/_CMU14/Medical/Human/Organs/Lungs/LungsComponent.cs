using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Human.Organs.Lungs;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedLungsSystem))]
public sealed partial class LungsComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Efficiency = 1.0f;

    /// <summary>
    ///     Per-stage asphyxiation damage (in Damage units) inflicted on the body
    ///     once per second while this lung sits at the given stage. Zero entries
    ///     mean "no self-damage at this stage".
    /// </summary>
    [DataField]
    public Dictionary<OrganDamageStatus, FixedPoint2> AsphyxPerSecond = new()
    {
        { OrganDamageStatus.None, FixedPoint2.Zero },
        { OrganDamageStatus.LittleBruised, FixedPoint2.Zero },
        { OrganDamageStatus.Bruised, FixedPoint2.Zero },
        { OrganDamageStatus.Broken, FixedPoint2.New(1) },
    };

    [DataField, AutoPausedField]
    public TimeSpan NextAsphyxTick;
}

[RegisterComponent]
[Access(typeof(SharedLungsSystem))]
public sealed partial class MissingLungsComponent : Component
{
    [DataField]
    public TimeSpan NextAsphyxTick;
}
