using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Yautja;

[Serializable, NetSerializable]
public enum CMUYautjaEffect : byte
{
    DiscReturn,
    CombiReturn,
    FalconRecall,
    PlasmaMuzzle,
    PlasmaImpact,
    Landing,
    ButcherCut,
    Roar,
    Flinch,
    Leap,
    FalconDeploy,
    CasterDeploy,
    Experiment,
}

[Serializable, NetSerializable]
public enum CMUYautjaPlasmaMode : byte
{
    Stun,
    Immobilizer,
    Lethal,
    Eradicator,
}

/// <summary>Server-timed presentation. These entities never deal damage or move gameplay items.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUYautjaEffectComponent : Component
{
    [DataField, AutoNetworkedField] public ProtoId<CMUYautjaExperimentPrototype>? Experiment;
    [DataField, AutoNetworkedField] public CMUYautjaEffect Kind;
    [DataField, AutoNetworkedField] public EntityUid? Target;
    [DataField, AutoNetworkedField] public EntityUid? Item;
    [DataField, AutoNetworkedField] public Vector2 Destination;
    [DataField, AutoNetworkedField] public Color Blood = Color.FromHex("#A01423");
    [DataField, AutoNetworkedField] public bool BadBloodDrone;
    [DataField, AutoNetworkedField] public int Stage;
    [DataField, AutoNetworkedField] public CMUYautjaPlasmaMode PlasmaMode;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan StartedAt;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan EndsAt;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUYautjaMotionComponent : Component
{
    [DataField, AutoNetworkedField] public CMUYautjaEffect Kind;
    [DataField, AutoNetworkedField] public EntityUid? Controller;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan StartedAt;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan EndsAt;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUYautjaOpticalCloakComponent : Component
{
    // Keep the selected preset on the wearer through forced decloak and bracer removal.
    [DataField, AutoNetworkedField]
    public YautjaInvisibilitySound Preset;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUYautjaPlasmaVisualComponent : Component
{
    [DataField, AutoNetworkedField] public CMUYautjaPlasmaMode Mode;
    [DataField, AutoNetworkedField] public Vector2 Origin;
    [DataField, AutoNetworkedField] public Vector2 Direction;
}

/// <summary>Completed wounds survive cancellation, PVS changes and reconnects.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUYautjaButcherVisualComponent : Component
{
    [DataField, AutoNetworkedField] public int CompletedStage;
    [DataField, AutoNetworkedField] public int ActiveStage;
    [DataField, AutoNetworkedField] public EntityUid? Hunter;
    [DataField, AutoNetworkedField] public YautjaButcherProcedure Procedure;
    [DataField, AutoNetworkedField] public bool Xeno;
    [DataField, AutoNetworkedField] public Color Blood;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan StartedAt;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan EndsAt;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUYautjaCasterAimComponent : Component
{
    [DataField, AutoNetworkedField] public EntityUid Caster;
    [DataField, AutoNetworkedField] public Vector2 Position;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan StartedAt;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan ExpiresAt;
}

[Serializable, NetSerializable]
public sealed class CMUYautjaCasterAimEvent(NetEntity caster, MapCoordinates position) : EntityEventArgs
{
    public readonly NetEntity Caster = caster;
    public readonly MapCoordinates Position = position;
}

public static class CMUYautjaPresentation
{
    public const float FalconHeight = 37f / 32f;
    public const float FalconDuration = 5.6f;
    public const float RecallDuration = 4.8f;
    public const float CloakDuration = 1.4f;
    public const float RoarDuration = 5.4f;

    public static float Progress(TimeSpan now, TimeSpan start, TimeSpan end) =>
        Math.Clamp((float) ((now - start).TotalSeconds / Math.Max(.001, (end - start).TotalSeconds)), 0, 1);

    public static float Smooth(float p) => p * p * (3 - 2 * p);
    public static float Segment(float p, float start, float end) => Smooth(Math.Clamp((p - start) / (end - start), 0, 1));

    public static Vector2 Recall(Vector2 from, Vector2 to, float p)
    {
        var lift = Segment(p, .16f, .29f);
        var travel = Segment(p, .30f, .74f);
        return Vector2.Lerp(from, to, travel) +
               new Vector2(0, MathF.Sin(travel * MathF.PI) * 19f / 32f +
                              lift * (1 - travel) * 17f / 32f);
    }

    public static float FalconUnfold(float p) => Segment(p, .53f, .76f);

    public static float RecallRotation(float p, bool stick)
    {
        var settle = 1 - Segment(p, .69f, .84f);
        return stick ? (-.6f + (MathF.PI * .17f + .6f) * Segment(p, .18f, .35f)) * settle
            : p < .16f ? 0 : p * 36 * settle;
    }

    // The four separately timed game procedures occupy quarters of the approved sixteen-second study.
    public static float ButcherPhase(int completed, int active, float progress) =>
        active > 0 ? (active - 1 + progress) / 4 : completed / 4f;

    public static float Random(int seed)
    {
        var value = Math.Sin(seed * 127.1 + 41.7) * 43758.5453;
        return (float) (value - Math.Floor(value));
    }
}
