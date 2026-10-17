using System.Numerics;
using Content.Shared.CMU14.Fighter;

namespace Content.Client.CMU14.Fighter;

public sealed partial class FighterClientSystem
{
    private TimeSpan? _airTrialIncoming;
    private bool _airTrialFlares;

    /// <summary>Opt-in two-aircraft trial using only the same network commands as the cockpit.</summary>
    private void UpdateAirCombatTrial(FighterAircraftComponent aircraft, FighterSeatComponent seat)
    {
        if (seat.Aircraft is not { } uid || !TryComp(uid, out FighterAirCombatComponent? combat) ||
            !TryComp(uid, out FighterWeaponsComponent? weapons)) return;
        var defender = FighterIFFSystem.Same(weapons.Faction, "opfor");
        var name = defender ? "air-defender" : "air-pilot";
        _trialStart ??= _timing.RealTime;
        var elapsed = (_timing.RealTime - _trialStart.Value).TotalSeconds;
        if (_trialStep == 0 && elapsed >= 6)
        {
            var bounds = aircraft.Battlefield;
            var first = FighterAirCombat.SectorAt(bounds, new Vector2(bounds.Center.X, bounds.Top - 1));
            var middle = FighterAirCombat.SectorAt(bounds, bounds.Center);
            if (!combat.CoveredSectors.Contains(first)) RaiseNetworkEvent(new FighterCoverSectorEvent(first));
            if (middle != first && !combat.CoveredSectors.Contains(middle)) RaiseNetworkEvent(new FighterCoverSectorEvent(middle));
            RaiseNetworkEvent(new FighterSettingsEvent(350, 26));
            _display?.ShowRoute(true, true);
            _trialStep = 1;
        }
        if (_trialStep == 1 && elapsed >= 15)
        {
            CaptureTrial(name + "-coverage");
            RaiseNetworkEvent(new FighterCommandEvent(FighterCommand.Launch));
            _trialStep = 2;
        }
        if (combat.Incoming)
        {
            _airTrialIncoming ??= _timing.RealTime;
            var warningTime = (_timing.RealTime - _airTrialIncoming.Value).TotalSeconds;
            if (!_airTrialFlares && warningTime >= 1)
            {
                CaptureTrial(name + "-warning");
                if (!_effectsSecondPass) RaiseNetworkEvent(new FighterCommandEvent(FighterCommand.Flares));
                _airTrialFlares = true;
            }
            if (_trialStep == 2 && warningTime >= 2 && combat.FlaresUsed)
            {
                CaptureTrial(name + "-flares");
                _trialStep = 3;
            }
        }
        if (_trialStep is 2 or 3 && combat.Result != FighterAirResult.None && !combat.Incoming)
        {
            CaptureTrial(name + "-result");
            _trialStep = 4;
        }
        var effectsTrial = _configuration.GetCVar(FighterTrialCVars.Effects);
        if (effectsTrial && !defender && _trialStep == 4 && !aircraft.Flying &&
            combat.Result == FighterAirResult.Evaded && !_effectsSecondPass)
        {
            // The second pass deliberately omits countermeasures so the native check
            // also reaches the real hit, damage-smoke and forced-return effects.
            _effectsSecondPass = true;
            _airTrialIncoming = null;
            _airTrialFlares = false;
            _trialStep = 2;
            RaiseNetworkEvent(new FighterCommandEvent(FighterCommand.Launch));
        }
        if (elapsed < (effectsTrial ? 240 : 120) && (defender || _trialStep < 4 || aircraft.Flying)) return;
        _trial = false;
        _configuration.SetCVar(FighterTrialCVars.Enabled, false);
        _input = FighterInput.None;
        Log.Info($"Fighter air-combat trial completed: {name}, result={combat.Result}, retreat={aircraft.ForcedRetreat}; manual controls restored.");
    }
}
