using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Marines.LastStand;

/// <summary>
/// Tracks a running Last Stand surge on its user. See <see cref="CMULastStandComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(CMULastStandSystem))]
public sealed partial class CMULastStandActiveComponent : Component
{
    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan ActivatedAt;

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan EndsAt;
}
