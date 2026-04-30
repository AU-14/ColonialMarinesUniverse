using Content.Shared._CMU14.Medical.Wounds;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Wounds;

/// <summary>
///     While present the part's passive heal tick is blocked and the bandage
///     picker excludes the part — debridement surgery is the only path back.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUEscharComponent : Component
{
    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan AppliedAt;
}
