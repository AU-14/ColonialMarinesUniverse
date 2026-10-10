using Content.Shared.CMU14.Fighter;

namespace Content.Client.CMU14.Fighter;

public sealed partial class FighterCockpitControl
{
    private void UpdateAirCombat(FighterAircraftComponent aircraft, FighterWeaponsComponent? weapons,
        FighterSeatComponent seat, FighterAirCombatComponent? combat, TimeSpan now)
    {
        AirCover.Disabled = combat == null || aircraft.GroundState != FighterGroundState.Airborne;
        AirCover.Lit = RoutePanel.Visible && _chart.CoverageMode;
        IncomingPanel.Visible = _airspace && combat != null && (combat.Incoming || now < combat.ResultUntil);
        Flares.Disabled = !_airspace || !seat.Pilot || combat is not { Incoming: true, FlaresUsed: false } || now >= combat.IncomingAt;
        Flares.Lit = !Flares.Disabled;
        Flares.StatusColor = Flares.Lit ? Color.Orange : null;
        Flares.ToolTip = !seat.Pilot ? Loc.GetString("cmu-fighter-flares-pilot")
            : combat is { Incoming: true }
                ? Loc.GetString("cmu-fighter-flares-chance", ("chance", MathF.Round((combat.FlaresUsed
                    ? combat.DeployedFlareEvasionChance
                    : FighterAirCombat.FlareEvasion(aircraft, combat, now)) * 100)))
                : Loc.GetString("cmu-fighter-flares-idle");
        IncomingFlares.Visible = combat is { Incoming: true };
        IncomingFlares.Disabled = Flares.Disabled;
        IncomingFlares.Lit = Flares.Lit;
        IncomingFlares.ToolTip = Flares.ToolTip;
        if (combat == null) return;

        if (aircraft.ForcedRetreat)
        {
            FlightHelp.Warning = true;
            FlightHelp.Text = Loc.GetString(combat.RecoveryUntil > now ? "cmu-fighter-repairing" : "cmu-fighter-retreating",
                ("seconds", Seconds(combat.RecoveryUntil - now)));
        }
        else FlightHelp.Warning = false;
        FlightDisplay.Warning = aircraft.ForcedRetreat;
        if (aircraft.ForcedRetreat) FlightDisplay.Text = FlightHelp.Text;
        if (aircraft.GroundState is FighterGroundState.Crashing or FighterGroundState.Crashed)
        {
            FlightHelp.Warning = FlightDisplay.Warning = true;
            FlightHelp.Text = FlightDisplay.Text = Loc.GetString(
                aircraft.GroundState == FighterGroundState.Crashing ? "cmu-fighter-crash-warning" : "cmu-fighter-crashed",
                ("seconds", Seconds(aircraft.GroundStateEndsAt - now)));
        }

        if (_chart.CoverageMode)
        {
            var status = aircraft.ForcedRetreat ? Loc.GetString("cmu-fighter-fire-retreat")
                : string.IsNullOrWhiteSpace(weapons?.Faction) ? Loc.GetString("cmu-fighter-cover-no-faction")
                : !FighterFlight.InAttackRun(aircraft) ? Loc.GetString("cmu-fighter-fire-outsideao")
                : now < combat.CoverageReadyAt ? Loc.GetString("cmu-fighter-cover-arming", ("seconds", Seconds(combat.CoverageReadyAt - now)))
                : now < combat.InterceptReadyAt ? Loc.GetString("cmu-fighter-cover-reloading", ("seconds", Seconds(combat.InterceptReadyAt - now)))
                : Loc.GetString("cmu-fighter-cover-ready");
            RouteStatus.Text = Loc.GetString("cmu-fighter-cover-status", ("count", combat.CoveredSectors.Count),
                ("max", combat.MaximumCoveredSectors), ("status", status));
        }

        IncomingFlares.Text = Loc.GetString(combat.FlaresUsed ? "cmu-fighter-flares-used" : seat.Pilot ? "cmu-fighter-flares" : "cmu-fighter-flares-pilot");
        if (combat.Incoming)
        {
            IncomingTitle.Text = Loc.GetString(combat.IncomingPlasma ? "cmu-fighter-plasma-incoming"
                : combat.IncomingFromGround ? "cmu-fighter-ground-incoming" : "cmu-fighter-air-incoming");
            IncomingStatus.Text = Loc.GetString("cmu-fighter-air-countdown",
                ("seconds", Math.Max(0, (combat.IncomingAt - now).TotalSeconds).ToString("0.0")));
        }
        else
        {
            IncomingTitle.Text = Loc.GetString(combat.Result == FighterAirResult.Evaded ? "cmu-fighter-air-evaded" : "cmu-fighter-air-hit");
            IncomingStatus.Text = Loc.GetString(combat.Result == FighterAirResult.Evaded ? "cmu-fighter-air-continue" : "cmu-fighter-retreating");
        }
    }

    private static int Seconds(TimeSpan time) => (int) Math.Ceiling(Math.Max(0, time.TotalSeconds));
}
