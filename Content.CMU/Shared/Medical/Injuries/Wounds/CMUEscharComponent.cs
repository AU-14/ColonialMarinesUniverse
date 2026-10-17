using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Medical.Injuries.Wounds;

/// <summary>
///     Burn eschar remains a distinct painful condition until debridement or
///     complete rejuvenation. Field dressings and tissue recovery can proceed
///     while it is present; healing damage does not itself remove the eschar.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUEscharComponent : Component
{
    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan AppliedAt;
}

[ByRefEvent]
public readonly record struct CMUEscharChangedEvent(EntityUid Body, EntityUid Part, bool Removed);
