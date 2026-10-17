using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Dropship;

/// <summary>
///     A dropship navigation computer that can open every door on its dropship and bolt them open.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUDropshipDoorsLockOpenComponent : Component
{
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(1);

    public TimeSpan LastUsed;
}

[Serializable, NetSerializable]
public sealed class CMUDropshipLockDoorsOpenMsg : BoundUserInterfaceMessage;
