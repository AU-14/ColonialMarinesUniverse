using Content.Shared.CMU14.Fighter;

namespace Content.Client.CMU14.Fighter;

public sealed partial class FighterClientSystem
{
    /// <summary>Opt-in native capture of a complete ground-to-holding-to-ground sortie.</summary>
    private void UpdateGroundTrial(FighterAircraftComponent aircraft, FighterSeatComponent seat)
    {
        _trialStart ??= _timing.RealTime;
        var elapsed = (_timing.RealTime - _trialStart.Value).TotalSeconds;
        var role = seat.Pilot ? "pilot" : "officer";
        if (_trialStep == 0 && elapsed >= 3)
        {
            _display?.ShowOptics(false);
            if (seat.Pilot) { _input = FighterInput.Forward; SendInput(); }
            _trialStep = 20;
        }
        if (_trialStep == 20 && elapsed >= 4)
        {
            if (seat.Pilot)
            {
                _input = FighterInput.None;
                SendInput();
                RaiseNetworkEvent(new FighterCommandEvent(FighterCommand.FormUp));
            }
            CaptureTrial($"ground-{role}-taxi");
            _trialStep = 21;
        }
        if (_trialStep == 21 && elapsed >= 8)
        {
            _display?.ShowOptics(false);
            CaptureTrial($"ground-{role}-ready");
            if (seat.Pilot) RaiseNetworkEvent(new FighterCommandEvent(FighterCommand.Takeoff));
            _trialStep = 1;
        }
        if (_trialStep == 1 && aircraft.GroundState == FighterGroundState.TakingOff)
        {
            _trialPassStart = _timing.RealTime;
            _trialStep = 2;
        }
        var phaseAge = _trialPassStart is { } phaseStart ? (_timing.RealTime - phaseStart).TotalSeconds : 0;
        if (_trialStep == 2 && phaseAge >= 1.2)
        {
            CaptureTrial($"ground-{role}-spool");
            _trialStep = 3;
        }
        if (_trialStep == 3 && phaseAge >= 3.3)
        {
            CaptureTrial($"ground-{role}-takeoff");
            _trialStep = 4;
        }
        if (_trialStep == 4 && phaseAge >= 6.1)
        {
            CaptureTrial($"ground-{role}-climb");
            _trialStep = 5;
        }
        if (_trialStep == 5 && aircraft.GroundState == FighterGroundState.Airborne)
        {
            _trialPassStart = _timing.RealTime;
            phaseAge = 0;
            _trialStep = 6;
        }
        if (_trialStep == 6 && phaseAge >= .4)
        {
            CaptureTrial($"ground-{role}-airborne");
            if (seat.Pilot) RaiseNetworkEvent(new FighterCommandEvent(FighterCommand.LeaveSeat));
            _trialStep = 7;
        }
        if (_trialStep == 7 && phaseAge >= 2)
        {
            CaptureTrial($"ground-{role}-eject-warning");
            if (seat.Pilot) RaiseNetworkEvent(new FighterCommandEvent(FighterCommand.CancelEject));
            _trialStep = 22;
        }
        if (_trialStep == 22 && phaseAge >= 5)
        {
            if (seat.Pilot) RaiseNetworkEvent(new FighterCommandEvent(FighterCommand.Return));
            _trialStep = 8;
        }
        if (_trialStep == 8 && aircraft.GroundState == FighterGroundState.Landing)
        {
            _trialPassStart = _timing.RealTime;
            phaseAge = 0;
            _trialStep = 9;
        }
        if (_trialStep == 9 && phaseAge >= 2.8)
        {
            CaptureTrial($"ground-{role}-landing");
            _trialStep = 10;
        }
        if (_trialStep == 10 && aircraft.GroundState == FighterGroundState.Grounded)
        {
            _trialPassStart = _timing.RealTime;
            phaseAge = 0;
            _trialStep = 11;
        }
        if (_trialStep == 11 && phaseAge >= .3)
        {
            CaptureTrial($"ground-{role}-touchdown");
            _trialStep = 12;
        }
        if (_trialStep == 12 && phaseAge >= 3.5)
        {
            CaptureTrial($"ground-{role}-landed");
            Log.Info("Fighter ground trial completed: takeoff, airborne holding, return and touchdown; manual controls restored.");
            _trial = false;
        }
        if (elapsed <= 75) return;
        Log.Warning($"Fighter ground trial timed out at {aircraft.GroundState}; manual controls restored.");
        _trial = false;
    }
}
