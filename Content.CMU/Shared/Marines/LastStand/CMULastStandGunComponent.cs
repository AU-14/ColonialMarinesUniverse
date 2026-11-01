using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Marines.LastStand;

/// <summary>
/// Marks a gun held by a user with an active Last Stand surge so its damage and accuracy
/// modifier events can be intercepted. Added and removed by <see cref="CMULastStandSystem"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(CMULastStandSystem))]
public sealed partial class CMULastStandGunComponent : Component;
