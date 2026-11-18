using System.Linq;
using Content.Server.GameTicking;
using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.Intel.Tech;
using Content.Shared._RMC14.Vehicle.Supply;
using Content.Shared.CMU14.Vehicle.Supply;
using Robust.Shared.Utility;

namespace Content.Server._RMC14.Vehicle;

public sealed partial class VehicleSupplySystem
{
    [Dependency] private GameTicker _vehicleTicker = default!;
    private const string TankResearch = "cmu-tech-insurgency-tank";

    private bool Insurgency => (_vehicleTicker.CurrentPreset ?? _vehicleTicker.Preset)?.ID == "Insurgency";

    private void OnIntelTreeCreated(ref CMUIntelTreeCreatedEvent ev)
    {
        if (!Insurgency || !string.Equals(ev.Team, Team.GovFor, StringComparison.OrdinalIgnoreCase) || ev.Tree.Options.Count <= 2)
            return;

        ev.Tree.Options[2].Add(new TechOption(TankResearch, "cmu-tech-insurgency-tank-desc",
            12, 0, false, 12, false, [new CMUTankRequisitionEvent()],
            new SpriteSpecifier.Rsi(new ResPath("_RMC14/Interface/tech_tree.rsi"), "budget_ds"), TimeSpan.Zero));
    }

    private bool ModeAllowsVehicle(string? side, VehicleSupplyEntry entry)
    {
        if (!Insurgency)
            return true;
        if (entry.Group is "vehicle-fighter" or "vehicle-vtol")
            return false;
        if (entry.Group != "vehicle-tank")
            return true;
        return side != null && _intel.TryGetTechTree(side, out var tree) &&
            tree.Value.Comp.Tree.Options.Count > 2 && tree.Value.Comp.Tree.Options[2].Any(option => option.Name == TankResearch && option.Purchased);
    }
}
