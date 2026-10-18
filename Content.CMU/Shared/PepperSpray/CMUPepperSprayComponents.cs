using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.PepperSpray;

/// <summary>
///     A spray can that's sprayed in someone's face. Each spray draws <see cref="PerSpray"/> from its solution, and
///     how much of that is <see cref="Reagent"/> sets how strong it is, so the can is refilled with chemistry.
///     <para/>
///     Pepper spray burns the eyes, blurring and tinting vision, and the pain builds up over a few seconds: two or
///     three good sprays in a row is enough to put someone down. The antidote (<see cref="Antidote"/>) washes it out.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUPepperSprayComponent : Component
{
    [DataField]
    public string Solution = "spray";

    [DataField(required: true)]
    public ProtoId<ReagentPrototype> Reagent;

    [DataField]
    public FixedPoint2 PerSpray = 4;

    [DataField]
    public bool Antidote;

    [DataField]
    public float Range = 2.5f;

    /// <summary>How far a burst sprayed at the floor or the air reaches.</summary>
    [DataField]
    public float BurstRange = 2f;

    /// <summary>Pain each spray causes in total, spread over <see cref="PainTicks"/> seconds.</summary>
    [DataField]
    public float Pain = 35f;

    [DataField]
    public int PainTicks = 5;

    /// <summary>How long the eyes keep burning after the last spray.</summary>
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(25);

    [DataField]
    public TimeSpan BlurTime = TimeSpan.FromSeconds(12);

    [DataField]
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/Effects/spray2.ogg");
}

/// <summary>
///     Someone whose eyes are burning from pepper spray.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
public sealed partial class CMUPepperSprayedComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan EndAt;

    /// <summary>How many sprays they've taken since their eyes last cleared up.</summary>
    [DataField, AutoNetworkedField]
    public int Sprays;

    /// <summary>
    ///     How much spray they've taken, weighted by how strong each spray was after protection. Each full-strength
    ///     spray adds 1. The slowdown builds with it, and at <see cref="StunLevel"/> they go down.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Intensity;

    [DataField]
    public float SlowPerIntensity = 0.15f;

    [DataField]
    public float MaxSlow = 0.45f;

    [DataField]
    public float StunLevel = 3f;

    [DataField]
    public TimeSpan StunTime = TimeSpan.FromSeconds(6);

    /// <summary>Whether they've already been knocked down by this round of spraying.</summary>
    [DataField]
    public bool Stunned;

    /// <summary>
    ///     How much of their current pain came from the spray, so the antidote can take that away and nothing else.
    ///     Wears off at the same rate pain does.
    /// </summary>
    [DataField]
    public float DeliveredPain;

    [DataField]
    public float PendingPain;

    [DataField]
    public float PainPerTick;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextPain;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextSplash;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextScream;
}
