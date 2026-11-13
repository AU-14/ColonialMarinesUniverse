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
}

/// <summary>
/// Marks a leader bracer: the one in the Leader loadouts, or a grunt's bracer promoted when its wearer ascends.
/// The ascension scuttle looks for this on the fallen Leader's bracer.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BadBloodLeaderBracerComponent : Component;

public sealed partial class BadBloodPitchCampActionEvent : InstantActionEvent;
