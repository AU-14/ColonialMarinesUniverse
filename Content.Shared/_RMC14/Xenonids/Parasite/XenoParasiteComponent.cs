using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._RMC14.Xenonids.Parasite;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
[Access(typeof(SharedXenoParasiteSystem))]
public sealed partial class XenoParasiteComponent : Component
{
    [DataField]
    public TimeSpan ManualAttachDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan SelfAttachDelay = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan ParalyzeTime = TimeSpan.FromMinutes(1.5);

    [DataField]
    public float InfectRange = 1.5f;

    [DataField]
    public EntityUid? InfectedVictim;

    /// <summary>
    ///     How long it takes for the parasite to fall off the victim's mask, finishing the infecting process.
    /// </summary>
    [DataField]
    public TimeSpan FallOffDelay = TimeSpan.FromSeconds(15);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? FallOffAt;

    [DataField]
    public bool FellOff;

    /// <summary>
    ///     Player who controlled this parasite when it successfully infected a host.
    /// </summary>
    public NetUserId? InfectorUser;

    /// <summary>
    ///     Whether the infector accepted becoming the larva from this infection.
    /// </summary>
    public bool InfectorWantsLarva;

    /// <summary>
    ///     Whether the infector has an unanswered larva claim prompt for this infection.
    /// </summary>
    public bool InfectorLarvaClaimPending;

    public int BaseTemporaryCollisionMask;

    public bool LeapCollisionActive;

    public bool ThrownCollisionActive;
}
