using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Fob;

/// <summary>FOB transport crates are opened with a held wrench.</summary>
[RegisterComponent, NetworkedComponent, Access(typeof(CMUFobCrateSystem))]
public sealed partial class CMUFobCrateComponent : Component
{
    [DataField] public TimeSpan OpenDuration = TimeSpan.FromSeconds(2);

    // Authorizes only the storage attempt made synchronously by the completed tool action.
    public EntityUid? OpeningUser;
}

[Serializable, NetSerializable]
public sealed partial class CMUFobCrateOpenDoAfterEvent : SimpleDoAfterEvent;
