using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Doors;

/// <summary>Ownership and explicit links for player-built podlocks, shutters, and buttons.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUConstructedDoorControlComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Builder;

    [DataField, AutoNetworkedField]
    public bool LinksLocked;

    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> LinkedDoors = new();
}

/// <summary>The button selected on a multitool, scoped to the selecting character.</summary>
[RegisterComponent]
public sealed partial class CMUDoorLinkToolComponent : Component
{
    public EntityUid? Button;
    public EntityUid? User;
}
