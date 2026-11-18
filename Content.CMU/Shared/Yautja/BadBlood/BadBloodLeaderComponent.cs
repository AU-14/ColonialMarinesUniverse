using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// The clan's current Leader. Only <c>BadBloodCampRuleSystem.SetLeader</c> adds or removes it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BadBloodLeaderComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId PitchCampAction = "CMUActionBadBloodPitchCamp";

    [DataField, AutoNetworkedField]
    public EntityUid? PitchCampActionEntity;

    /// <summary>
    /// CMU14 ECS-611: the Leader stands a little taller than the rest of the clan, so marines can pick them out.
    /// It follows leadership: the old Leader returns to normal size.
    /// </summary>
    [DataField]
    public float SpriteScale = DefaultSpriteScale;

    public const float DefaultSpriteScale = 1.1f;
}

/// <summary>
/// CMU14 ECS-611: this body was enlarged for leading the clan, so the size comes off with leadership.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodLeaderScaledComponent : Component;

/// <summary>
/// Marks a leader bracer: the one in the Leader loadouts, or a grunt's bracer promoted when its wearer ascends.
/// The ascension scuttle looks for this on the fallen Leader's bracer.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BadBloodLeaderBracerComponent : Component;

public sealed partial class BadBloodPitchCampActionEvent : InstantActionEvent;
