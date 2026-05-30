using Content.Server._RMC14.Requisitions;
using Content.Shared._RMC14.Overwatch;
using Content.Shared._RMC14.SupplyDrop;
using Content.Shared.Popups;

namespace Content.Server._RMC14.SupplyDrop;

public sealed class SupplyDropSystem : SharedSupplyDropSystem
{
    [Dependency] private RequisitionsSystem _requisitions = default!;
    [Dependency] private SharedPopupSystem _serverPopup = default!;

    public override bool TryLaunchSupplyDropPopup(Entity<SupplyDropComputerComponent> computer, EntityUid user)
    {
        if (computer.Comp.Cost > 0 && TryComp(computer, out OverwatchConsoleComponent? console))
        {
            var faction = string.IsNullOrEmpty(console.Group) ? string.Empty : console.Group;
            if (!_requisitions.TrySpendFunds(faction, computer.Comp.Cost))
            {
                _serverPopup.PopupCursor(
                    Loc.GetString("rmc-supply-drop-insufficient-funds", ("cost", computer.Comp.Cost)),
                    user,
                    PopupType.MediumCaution);
                return false;
            }
        }

        return base.TryLaunchSupplyDropPopup(computer, user);
    }
}
