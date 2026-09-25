using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.Fighter;

namespace Content.Client.CMU14.Fighter;

public sealed partial class FighterClientSystem
{
    private void UpdateWeaponTrial(FighterAircraftComponent aircraft, FighterSeatComponent seat)
    {
        if (seat.Aircraft is not { } uid || !TryComp(uid, out FighterWeaponsComponent? weapons)) return;
        _trialStart ??= _timing.RealTime;
        var elapsed = (_timing.RealTime - _trialStart.Value).TotalSeconds;
        if (_trialStep == 0 && elapsed >= 8 && weapons.Targets.Count > 0)
        {
            var flare = seat.Pilot ? weapons.Targets.Last() : weapons.Targets.First();
            RaiseNetworkEvent(new FighterSelectTargetEvent(flare.Id));
            RaiseNetworkEvent(new FighterSelectWeaponEvent(seat.Pilot ? 2 : 0));
            _display?.ShowOptics(true);
            if (!seat.Pilot) RaiseNetworkEvent(new FighterCommandEvent(FighterCommand.VisionNight));
            if (seat.Pilot)
            {
                RaiseNetworkEvent(new FighterCommandEvent(FighterCommand.PrepareRun));
            }
            _trialStep = 1;
        }
        if (_trialStep == 1 && elapsed >= 10)
        {
            CaptureTrial(seat.Pilot ? "weapons-pilot-holding" : "weapons-officer-holding");
            if (seat.Pilot) RaiseNetworkEvent(new FighterCommandEvent(FighterCommand.QueueFire));
            _trialStep = 2;
        }
        if (_trialStep == 2 && elapsed >= 18)
        {
            CaptureTrial(seat.Pilot ? "weapons-pilot-launch" : "weapons-officer-ready");
            if (seat.Pilot) RaiseNetworkEvent(new FighterCommandEvent(FighterCommand.Launch));
            _trialStep = 3;
        }
        if (_trialStep == 3 && !seat.Pilot && FighterWeapons.Status(aircraft, weapons, seat,
                weapons.Loadout.FirstOrDefault(slot => slot.Slot == seat.WeaponSlot),
                weapons.Targets.FirstOrDefault(target => target.Id == seat.Target), _timing.CurTime) == FighterFireStatus.Ready)
        {
            RaiseNetworkEvent(new FighterCommandEvent(FighterCommand.Fire));
            _trialStep = 7;
        }
        if (_trialStep == 3 && seat.Pilot)
        {
            if (TryComp(uid, out FighterEffectsComponent? effects) && effects.Cues.Any(cue => cue.Kind == FighterEffectKind.Rocket))
            {
                CaptureTrial("weapons-rocket-run");
                _trialPassStart = _timing.RealTime;
                _trialStep = 4;
            }
        }
        if (_trialStep == 4 && _trialPassStart is { } shot && (_timing.RealTime - shot).TotalSeconds >= 3)
        {
            CaptureTrial("weapons-rocket-fired");
            RaiseNetworkEvent(new FighterSelectWeaponEvent(FighterWeaponsComponent.GauSlot));
            _trialStep = 5;
        }
        if (_trialStep == 5 && _trialPassStart is { } fired && (_timing.RealTime - fired).TotalSeconds >= 4)
        {
            RaiseNetworkEvent(new FighterCommandEvent(FighterCommand.QueueFire));
            _trialStep = 6;
        }
        if (elapsed >= 100)
        {
            CaptureTrial(seat.Pilot ? "weapons-pilot-end" : "weapons-officer-end");
            if (seat.Pilot) RaiseNetworkEvent(new FighterCommandEvent(FighterCommand.Return));
            _trial = false;
            _configuration.SetCVar(FighterTrialCVars.Enabled, false);
            Log.Info($"Fighter weapons trial finished at step {_trialStep}; manual controls restored.");
        }
    }
}
