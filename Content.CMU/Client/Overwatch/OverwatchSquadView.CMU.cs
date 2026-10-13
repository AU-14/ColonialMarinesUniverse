using Content.Client.CMU14.Overwatch;

namespace Content.Client._RMC14.Overwatch;

// CMU14: the console's tab column lights the open tab (CRT terminal look).
public sealed partial class OverwatchSquadView
{
    private void CMUOnTabShown()
    {
        CMUOverwatchCrt.SetLit(SquadMonitorButton, SquadMonitor.Visible);
        CMUOverwatchCrt.SetLit(SupplyDropButton, SupplyDrop.Visible);
        CMUOverwatchCrt.SetLit(OrbitalBombardmentButton, OrbitalBombardment.Visible);
        CMUOverwatchCrt.SetLit(FireteamsButton, Fireteams.Visible);
    }
}
