using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Medical.Synth;

// lives on every synth with a bloodstream, added by CMUSynthCirculationSystem on MapInit
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUSynthCirculationSystem))]
public sealed partial class CMUSynthCirculationComponent : Component
{
    // per welder pass, applied to every organ that's hurt but not destroyed
    [DataField, AutoNetworkedField]
    public FixedPoint2 OrganRepairAmount = 15;

    [DataField, AutoNetworkedField]
    public FixedPoint2 OrganRepairFuel = 5;

    [DataField, AutoNetworkedField]
    public TimeSpan OrganRepairTime = TimeSpan.FromSeconds(7);

    [DataField, AutoNetworkedField]
    public TimeSpan OrganSelfRepairTime = TimeSpan.FromSeconds(30);
}

[Serializable, NetSerializable]
public sealed partial class CMUSynthOrganRepairDoAfterEvent : SimpleDoAfterEvent;
