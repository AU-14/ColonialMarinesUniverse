using Content.Client.CMU14.Overwatch;
using Content.Client.CMU14.ShipWeapons;

namespace Content.Client._RMC14.Overwatch;

// CMU14: the console's tab column lights the open tab (CRT terminal look).
public sealed partial class OverwatchSquadView
{
    public bool CMUShipWeaponDepleted;
    public CMUShipWeaponPicker? CMUWeaponPicker;

    public bool CMUSquadInfoHidden => _squadInfoHidden;

    public void CMUSetSquadInfoHidden(bool hidden)
    {
        if (_squadInfoHidden == hidden)
            return;

        _squadInfoHidden = hidden;
        ApplySquadInfoVisibilityState();
        HideSquadInfoButton.Text = hidden
            ? Loc.GetString("rmc-overwatch-console-show-squad-info")
            : Loc.GetString("rmc-overwatch-console-hide-squad-info");
    }

    private void CMUOnTabShown()
    {
        CMUOverwatchCrt.SetLit(SquadMonitorButton, SquadMonitor.Visible);
        CMUOverwatchCrt.SetLit(SupplyDropButton, SupplyDrop.Visible);
        CMUOverwatchCrt.SetLit(OrbitalBombardmentButton, OrbitalBombardment.Visible);
        CMUOverwatchCrt.SetLit(FireteamsButton, Fireteams.Visible);
    }
}
