using Content.Shared.Chemistry.Reagent;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Weapons.Ranged.Flamer;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedRMCFlamerSystem))]
public sealed partial class RMCFlamerTankComponent : Component
{
    [DataField, AutoNetworkedField]
    public string SolutionId = "rmc_flamer_tank";

    [DataField, AutoNetworkedField]
    public EntityWhitelist? RefillWhitelist;

    [DataField, AutoNetworkedField]
    public int MaxIntensity = 40;

    [DataField, AutoNetworkedField]
    public int MaxDuration = 30;

    [DataField, AutoNetworkedField]
    public int MaxRange = 5;

    [DataField, AutoNetworkedField]
    public bool IgnoreReagentRange;

    [DataField, AutoNetworkedField]
    public string ExamineIcon = "/Textures/_RMC14/Structures/Storage/reagent_tank.rsi/weldtank.png";

    [DataField, AutoNetworkedField]
    public List<ProtoId<ReagentPrototype>>? ReagentWhitelist = null;

    // CMU14 start
    /// <summary>
    /// Whether this tank can fire blue fire (<see cref="SharedRMCFlamerSystem.BlueFire"/> or a fire entity
    /// inheriting from it). Only specialist tanks (M240-T, FL3 and equivalents) allow it; any other tank loaded
    /// with a blue-fire fuel fires <see cref="FallbackFire"/> instead.
    /// FLOW-22: blue is now the colour of intensity 40+, so this also lets the tank exceed intensity 39; every other
    /// tank is capped at 39 by <c>CMUFuelProfileSystem.TryResolveFire</c> whatever its <see cref="MaxIntensity"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AllowBlueFire;

    /// <summary>Fire entity used in place of a blue fire this tank does not allow.</summary>
    [DataField, AutoNetworkedField]
    public EntProtoId FallbackFire = "RMCTileFire";

    /// <summary>
    /// Share of its capped reach (fuel range limited by <see cref="MaxRange"/>) this tank projects, rounded half away
    /// from zero and never below 1. Independent of <see cref="AllowBlueFire"/>: M240 tanks project their whole reach,
    /// the civilian LC9 colony tank 0.6 of it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ReachFraction = 1f;

    /// <summary>
    /// Widest row of this tank's flame cone (<c>CMUFlameConeSystem</c>): 3 for standard tanks, 5 for specialist and
    /// vehicle tanks. Independent of <see cref="AllowBlueFire"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int ConeMaxWidth = 3;
    // CMU14 end
}
