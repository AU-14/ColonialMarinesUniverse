using Content.Shared._RMC14.Line;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameStates;
using Robust.Shared.Map; // CMU14
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Weapons.Ranged.Flamer;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedRMCFlamerSystem))]
public sealed partial class RMCFlamerChainComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId Spawn = "RMCTileFire";

    [DataField, AutoNetworkedField]
    public List<LineTile> Tiles = new();

    [DataField, AutoNetworkedField]
    public EntityCoordinates Origin; // CMU14: recheck obstruction while the spray travels.

    [DataField, AutoNetworkedField]
    public ProtoId<ReagentPrototype> Reagent = "RMCNapalmUT";

    // CMU14 start - fire sizing from the blended fuel profile; 0 falls back to the reagent's own stats.
    [DataField, AutoNetworkedField]
    public int Intensity;

    [DataField, AutoNetworkedField]
    public int Duration;

    /// <summary>Blend carries enough Firepenetrating fuel for its fires to ignore heat resistance.</summary>
    [DataField, AutoNetworkedField]
    public bool FirePenetrating;
    // CMU14 end

    [DataField, AutoNetworkedField]
    public int MaxIntensity = 20;

    [DataField, AutoNetworkedField]
    public int MaxDuration = 24;
}
