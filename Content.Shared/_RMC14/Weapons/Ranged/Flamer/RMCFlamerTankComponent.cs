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
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AllowBlueFire;

    /// <summary>Fire entity used in place of a blue fire this tank does not allow.</summary>
    [DataField, AutoNetworkedField]
    public EntProtoId FallbackFire = "RMCTileFire";
    // CMU14 end
}
