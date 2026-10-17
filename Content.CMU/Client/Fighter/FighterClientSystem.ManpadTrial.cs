using System.Numerics;
using Content.Shared.CMU14.Fighter;

namespace Content.Client.CMU14.Fighter;

public sealed partial class FighterClientSystem
{
    private int _manpadCaptureStep;

    /// <summary>Opt-in native preview: watch the ground launch, fly one pass and use the ordinary flare command.</summary>
    private void UpdateManpadTrial(FighterAircraftComponent aircraft, FighterSeatComponent seat)
    {
        if (seat.Aircraft is not { } uid || !TryComp(uid, out FighterWeaponsComponent? weapons) ||
            !TryComp(uid, out FighterAirCombatComponent? combat)) return;
        _trialStart ??= _timing.RealTime;
        var elapsed = (_timing.RealTime - _trialStart.Value).TotalSeconds;
        var role = seat.Pilot ? "pilot" : "officer";
        if (_trialStep == 0 && elapsed >= 6 && weapons.Targets.Count > 0)
        {
            var target = weapons.Targets[^1];
            RaiseNetworkEvent(new FighterSelectTargetEvent(target.Id));
            _display?.ShowOptics(!seat.Pilot);
            if (seat.Pilot)
            {
                RaiseNetworkEvent(new FighterPlanEvent(target.Position + new Vector2(120, 0), target.Position - new Vector2(120, 0)));
                RaiseNetworkEvent(new FighterSettingsEvent(350, 26));
            }
            _trialStep = 1;
        }
        if (_trialStep == 1 && elapsed >= 10)
        {
            CaptureTrial($"manpad-{role}-ready");
            if (seat.Pilot) RaiseNetworkEvent(new FighterCommandEvent(FighterCommand.Launch));
            _trialStep = 2;
        }

        if (!seat.Pilot && _manpadCaptureStep < 4)
        {
            var query = EntityQueryEnumerator<FighterManpadVisualComponent, TransformComponent>();
            while (query.MoveNext(out _, out var launch, out var xform))
            {
                if (xform.MapUid != aircraft.TerrainMap) continue;
                var age = (_timing.CurTime - launch.StartedAt).TotalSeconds;
                if (!launch.Launched)
                {
                    if (_manpadCaptureStep == -1 && age >= .35)
                    {
                        CaptureTrial("manpad-ground-windup");
                        _manpadCaptureStep = 0;
                    }
                    continue;
                }
                if (_manpadCaptureStep == -1) _manpadCaptureStep = 0;
                var deadline = _manpadCaptureStep switch { 0 => .12, 1 => .65, 2 => 1.25, _ => 2.5 };
                if (age < deadline || _timing.CurTime >= launch.ExpiresAt) continue;
                CaptureTrial($"manpad-ground-{_manpadCaptureStep++}");
                break;
            }
        }
        if (combat.Incoming)
        {
            _airTrialIncoming ??= _timing.RealTime;
            var age = (_timing.RealTime - _airTrialIncoming.Value).TotalSeconds;
            if (!_airTrialFlares && age >= 1)
            {
                CaptureTrial($"manpad-{role}-incoming");
                if (seat.Pilot) RaiseNetworkEvent(new FighterCommandEvent(FighterCommand.Flares));
                _airTrialFlares = true;
            }
            if (_trialStep == 2 && age >= 1.9)
            {
                CaptureTrial($"manpad-{role}-flares");
                _trialStep = 3;
            }
        }
        if (_trialStep is 2 or 3 && !combat.Incoming && combat.Result != FighterAirResult.None)
        {
            CaptureTrial($"manpad-{role}-result");
            if (seat.Pilot) RaiseNetworkEvent(new FighterCommandEvent(FighterCommand.Return));
            _trialStep = 4;
        }
        if (elapsed < 120 && (_trialStep < 4 || aircraft.Flying)) return;
        _trial = false;
        _configuration.SetCVar(FighterTrialCVars.Enabled, false);
        _input = FighterInput.None;
        Log.Info($"MANPAD preview completed: {role}, result={combat.Result}; manual controls restored.");
    }
}
