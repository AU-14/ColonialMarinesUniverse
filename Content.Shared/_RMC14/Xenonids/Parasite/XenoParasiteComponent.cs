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

    // cmu edit start: a parasite holds on for two to five minutes; after two, shakes and pats can knock it loose
    [DataField("cmuMinHugTime")]
    public TimeSpan CMUMinHugTime = TimeSpan.FromMinutes(2);

    [DataField("cmuMaxHugTime")]
    public TimeSpan CMUMaxHugTime = TimeSpan.FromMinutes(5);

    [DataField("cmuShakeOffChance")]
    public float CMUShakeOffChance = 0.25f;

    [DataField("cmuAttachedAt", customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? CMUAttachedAt;
    // cmu edit end

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
