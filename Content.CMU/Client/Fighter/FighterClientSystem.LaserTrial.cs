using System.Linq;
using Content.Shared.CMU14.Fighter;

namespace Content.Client.CMU14.Fighter;

public sealed partial class FighterClientSystem
{
    private TimeSpan _laserTrialNextAction;

    private void UpdateLaserTrial(FighterAircraftComponent aircraft, FighterSeatComponent seat)
    {
        if (seat.Aircraft is not { } uid || !TryComp(uid, out FighterWeaponsComponent? weapons)) return;
        _trialStart ??= _timing.RealTime;
        var elapsed = (_timing.RealTime - _trialStart.Value).TotalSeconds;
        var role = seat.Pilot ? "pilot" : "officer";
        if (_trialStep == 0 && elapsed >= 8 && _timing.RealTime >= _laserTrialNextAction &&
            weapons.Targets.FirstOrDefault(target => !target.Laser) is { } flare)
        {
            _laserTrialNextAction = _timing.RealTime + TimeSpan.FromSeconds(1);
            _display?.ShowOptics(true);
            if (seat.Target == flare.Id) _trialStep = 1;
            else
            {
                RaiseNetworkEvent(new FighterSelectTargetEvent(flare.Id));
                RaiseNetworkEvent(new FighterSelectWeaponEvent(0));
            }
        }
        if (_trialStep == 1 && elapsed >= 10)
        {
            CaptureTrial("laser-" + role + "-clear-camera");
            if (seat.Pilot) RaiseNetworkEvent(new FighterCommandEvent(FighterCommand.PrepareRun));
            _laserTrialNextAction = _timing.RealTime + TimeSpan.FromSeconds(2);
            _trialStep = 2;
        }
        if (_trialStep == 2 && seat.Pilot && _timing.RealTime >= _laserTrialNextAction)
        {
            _laserTrialNextAction = _timing.RealTime + TimeSpan.FromSeconds(2);
            if (!aircraft.Flying) RaiseNetworkEvent(new FighterCommandEvent(FighterCommand.Launch));
            else if (FighterFlight.InAttackRun(aircraft)) RaiseNetworkEvent(new FighterCommandEvent(FighterCommand.Laser));
        }
        if (_trialStep == 2 && weapons.Targets.FirstOrDefault(target => target.Laser) is { } laser)
        {
            if (!seat.Pilot) RaiseNetworkEvent(new FighterSelectTargetEvent(laser.Id));
            _trialPassStart = _timing.RealTime;
            _trialStep = 3;
        }
        if (_trialPassStart is { } start)
        {
            var active = (_timing.RealTime - start).TotalSeconds;
            if (_trialStep == 3 && active >= 1)
            {
                CaptureTrial("laser-" + role + "-visible");
                if (seat.Pilot) RaiseNetworkEvent(new FighterCommandEvent(FighterCommand.Fire));
                _trialStep = 4;
            }
            if (_trialStep == 4 && active >= 1.8)
            {
                CaptureTrial("laser-" + role + "-locking");
                _trialStep = 5;
            }
            if (_trialStep == 5 && seat.Target is { } id && TryGetEntity(id, out var target) &&
                TryComp(target, out FighterLaserComponent? mark) && mark.Incoming &&
                _timing.CurTime >= mark.IncomingAt + TimeSpan.FromSeconds(1.5))
            {
                CaptureTrial("laser-" + role + "-launched");
                Log.Info($"Fighter laser trial {role}: rounds after lock = {weapons.Loadout.FirstOrDefault(slot => slot.Slot == 0)?.Rounds}.");
                Log.Info($"Fighter laser trial {role}: world incoming = {mark.Incoming}, warning age = {(_timing.CurTime - mark.IncomingAt).TotalSeconds:F2}s.");
                if (_overlays.TryGetOverlay<FighterLaserOverlay>(out var overlay))
                    Log.Info($"Fighter laser trial {role}: incoming warning rendered = {overlay.LastIncomingDrawAt >= mark.IncomingAt}.");
                _trialStep = 6;
            }
            if (_trialStep == 6 && active >= 10.6 && weapons.Targets.All(target => !target.Laser))
            {
                CaptureTrial("laser-" + role + "-expired");
                Log.Info($"Fighter laser trial {role}: live lasers after expiry = {weapons.Targets.Count(target => target.Laser)}.");
                if (weapons.Targets.FirstOrDefault(target => !target.Laser) is { } restore)
                    RaiseNetworkEvent(new FighterSelectTargetEvent(restore.Id));
                _trialStep = 7;
            }
        }
        if (_trialStep == 7 || elapsed >= 75)
        {
            _trial = false;
            _configuration.SetCVar(FighterTrialCVars.Enabled, false);
            Log.Info($"Fighter laser trial {role} finished at step {_trialStep}; manual controls restored.");
        }
    }
}
