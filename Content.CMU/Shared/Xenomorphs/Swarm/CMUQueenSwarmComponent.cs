using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Xenomorphs.Swarm;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUQueenSwarmComponent : Component
{
    /// <summary>Null follows the round mode (Distress Signal only); admins can override this per queen through VV.</summary>
    [DataField]
    public bool? Enabled;

    [DataField] public EntProtoId MenuAction = "CMUActionSwarmMenu";
    [DataField] public EntProtoId RulesAction = "CMUActionSwarmRules";
    [DataField] public EntProtoId SummonAction = "CMUActionSummonSwarm";
    [DataField] public EntProtoId GuardAction = "CMUActionGuardSwarm";
    [DataField] public EntProtoId FollowAction = "CMUActionFollowSwarm";
}

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUSwarmLesserComponent : Component;

public sealed partial class CMUSummonSwarmActionEvent : InstantActionEvent;
public sealed partial class CMUSwarmMenuActionEvent : InstantActionEvent;
public sealed partial class CMUSwarmRulesActionEvent : InstantActionEvent;
public sealed partial class CMUGuardSwarmActionEvent : WorldTargetActionEvent;
public sealed partial class CMUFollowSwarmActionEvent : EntityTargetActionEvent;
