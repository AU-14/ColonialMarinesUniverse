using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._CMU14.Threats.Mobs.Abomination.Abilities;

/// <summary>
///     Lives on a skitter (or other builder abomination). Holds the list of
///     structures it knows how to secrete plus its currently-selected build
///     choice. The choose action opens a BUI; the secrete action spawns the
///     stored choice at the target tile.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class AbominationConstructionComponent : Component
{
    /// <summary>Currently-selected structure to place. null = nothing chosen yet.</summary>
    [DataField, AutoNetworkedField]
    public EntProtoId? BuildChoice;

    /// <summary>Structures the builder can secrete.</summary>
    [DataField, AutoNetworkedField]
    public List<EntProtoId> CanBuild = new();

    [DataField, AutoNetworkedField]
    public TimeSpan NestCooldown = TimeSpan.FromSeconds(40);

    /// <summary>
    ///     Specific prototype that gets a per-placement cooldown — defaults to the
    ///     flesh nest spawner. Other structures are gated only by the action's
    ///     useDelay.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId NestProto = "AU14AbominationFleshNest";

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan? NextNestAt;
}

public sealed partial class AbominationConstructionChooseActionEvent : InstantActionEvent;
public sealed partial class AbominationConstructionSecreteActionEvent : WorldTargetActionEvent;

[Serializable, NetSerializable]
public sealed class AbominationConstructionChooseMessage : BoundUserInterfaceMessage
{
    public AbominationConstructionChooseMessage(EntProtoId structure) => Structure = structure;
    public EntProtoId Structure { get; }
}

[Serializable, NetSerializable]
public sealed class AbominationConstructionBuiState : BoundUserInterfaceState
{
    public AbominationConstructionBuiState(List<string> options, string? selected)
    {
        Options = options;
        Selected = selected;
    }

    public List<string> Options { get; }
    public string? Selected { get; }
}

[Serializable, NetSerializable]
public enum AbominationConstructionUiKey : byte
{
    Key
}
