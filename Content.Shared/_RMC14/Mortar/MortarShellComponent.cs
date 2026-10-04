using Robust.Shared.GameStates;

using Content.Shared.Maps;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Mortar;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedMortarSystem))]
public sealed partial class MortarShellComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan LoadDelay = TimeSpan.FromSeconds(1.5);

    [DataField, AutoNetworkedField]
    public TimeSpan TravelDelay = TimeSpan.FromSeconds(4.5);

    [DataField, AutoNetworkedField]
    public TimeSpan WarshipTravelDelay = TimeSpan.FromSeconds(0.5);

    [DataField, AutoNetworkedField]
    public TimeSpan ImpactWarningDelay = TimeSpan.FromSeconds(2.5);

    [DataField, AutoNetworkedField]
    public TimeSpan ImpactDelay = TimeSpan.FromSeconds(4.5);

    [DataField, AutoNetworkedField]
    public bool CarvesZLevels;

    [DataField, AutoNetworkedField]
    public bool CreatesZLevelOpening;

    [DataField, AutoNetworkedField]
    public ProtoId<ContentTileDefinition>? CarveOpeningTile = ContentTileDefinition.SpaceID;

    [DataField, AutoNetworkedField]
    public ProtoId<ContentTileDefinition>? CarveFringeTile = "Lattice";

    [DataField, AutoNetworkedField]
    public int CarveFringeRadius;

    [DataField, AutoNetworkedField]
    public TimeSpan CarveDelay = TimeSpan.FromSeconds(0.25);

    [DataField, AutoNetworkedField]
    public int MaxCarveImpacts = 8;
}
