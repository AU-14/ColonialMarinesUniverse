using Content.Shared.Interaction;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Ladder;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class LadderComponent : Component
{
    [DataField, AutoNetworkedField]
    public string? Id;

    [DataField, AutoNetworkedField]
    public EntityUid? Other;

    [DataField, AutoNetworkedField]
    public TimeSpan Delay = TimeSpan.FromSeconds(2);

    [DataField, AutoNetworkedField]
    public float Range = SharedInteractionSystem.InteractionRange + 0.1f;

    // The last climber can be deleted while its do-after is being cancelled.
    // Keep a weak network reference rather than resolving a deleted entity during state generation.
    [ViewVariables, AutoNetworkedField]
    public NetEntity? LastDoAfterEnt;

    [DataField, AutoNetworkedField]
    public ushort? LastDoAfterId;

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan LastDoAfterTime;

    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> Watching = new();
}
