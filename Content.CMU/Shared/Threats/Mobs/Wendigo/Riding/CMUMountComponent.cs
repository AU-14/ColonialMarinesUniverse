using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Threats.Mobs.Wendigo.Riding;

/// <summary>
///     A mob that carries up to two riders on its shoulders, each only with its consent.
///     Needs a <c>Strap</c> sized for two riders; every buckle onto it without a grant from
///     <see cref="CMUMountSystem.TryMount"/> is refused.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUMountSystem))]
public sealed partial class CMUMountComponent : Component
{
    /// <summary>Left shoulder seat, relative to the mount and kept upright as it turns.</summary>
    [DataField, AutoNetworkedField]
    public Vector2 LeftSeat = new(-0.3f, 0.45f);

    /// <summary>Right shoulder seat, relative to the mount and kept upright as it turns.</summary>
    [DataField, AutoNetworkedField]
    public Vector2 RightSeat = new(0.3f, 0.45f);

    [DataField, AutoNetworkedField]
    public EntityUid? LeftRider;

    [DataField, AutoNetworkedField]
    public EntityUid? RightRider;

    /// <summary>How long the mount has to answer a request to climb on.</summary>
    [DataField]
    public TimeSpan ConsentTimeout = TimeSpan.FromSeconds(15);

    /// <summary>How long a rider stays down after being thrown off.</summary>
    [DataField]
    public TimeSpan DismountKnockdown = TimeSpan.FromSeconds(1.5);

    [DataField]
    public float Range = 1.5f;

    /// <summary>The entity whose request is waiting for an answer, if any.</summary>
    [ViewVariables]
    public EntityUid? PendingRequester;

    [ViewVariables]
    public TimeSpan PendingUntil;

    /// <summary>One-shot grant, set only for the duration of the consented buckle.</summary>
    [ViewVariables]
    public EntityUid? ConsentGrant;
}

[Serializable, NetSerializable]
public enum CMUMountSeat : byte
{
    Left,
    Right,
}

/// <summary>The mount's answer to a request to climb on. Raised by the consent dialog.</summary>
[Serializable, NetSerializable]
public sealed record CMUMountConsentEvent(NetEntity Mount, NetEntity Rider, bool Accept);
