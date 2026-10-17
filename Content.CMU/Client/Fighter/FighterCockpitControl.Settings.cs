using Content.Shared.CMU14.Fighter;

namespace Content.Client.CMU14.Fighter;

public sealed partial class FighterCockpitControl
{
    private void SelectAltitude(float height)
    {
        if (_pilotRole != true || _aircraft is not { ForcedRetreat: false } aircraft) return;
        _selectedHeight = Math.Clamp(height, FighterFlight.MinimumHeight, FighterFlight.MaximumHeight);
        _settings(_selectedHeight, _selectedSpeed);
        RefreshPresetReadouts(aircraft);
    }

    private void SelectSpeed(float speed)
    {
        if (_pilotRole != true || _aircraft is not { ForcedRetreat: false } aircraft) return;
        _selectedSpeed = Math.Clamp(speed, aircraft.MinimumSpeed, aircraft.MaximumSpeed);
        _settings(_selectedHeight, _selectedSpeed);
        RefreshPresetReadouts(aircraft);
    }

    private void UpdatePresets(FighterAircraftComponent aircraft, bool pilot)
    {
        var disabled = !pilot || aircraft.ForcedRetreat;
        LowAltitude.Disabled = MediumAltitude.Disabled = HighAltitude.Disabled = disabled;
        NearStall.Disabled = Cruise.Disabled = Afterburner.Disabled = disabled;
        // Do not overwrite a click with the unchanged network snapshot before
        // the server receives it. Keep both choices when presets are clicked in succession.
        if (_receivedHeight != aircraft.TargetHeight)
            _selectedHeight = _receivedHeight = aircraft.TargetHeight;
        if (_receivedSpeed != aircraft.TargetSpeed)
            _selectedSpeed = _receivedSpeed = aircraft.TargetSpeed;
        RefreshPresetReadouts(aircraft);
    }

    private void RefreshPresetReadouts(FighterAircraftComponent aircraft)
    {
        LowAltitude.Lit = _selectedHeight < FighterFlight.CloudBase;
        MediumAltitude.Lit = _selectedHeight >= FighterFlight.CloudBase && _selectedHeight <= FighterFlight.CloudTop;
        HighAltitude.Lit = _selectedHeight > FighterFlight.CloudTop;
        var cruise = Math.Clamp(12, aircraft.MinimumSpeed, aircraft.MaximumSpeed);
        var slowBoundary = (aircraft.MinimumSpeed + cruise) / 2;
        var fastBoundary = (cruise + aircraft.MaximumSpeed) / 2;
        NearStall.Lit = _selectedSpeed < slowBoundary;
        Cruise.Lit = _selectedSpeed >= slowBoundary && _selectedSpeed <= fastBoundary;
        Afterburner.Lit = _selectedSpeed > fastBoundary;
        HeightLabel.Text = Loc.GetString("cmu-fighter-height", ("height", MathF.Round(_selectedHeight)));
        SpeedLabel.Text = Loc.GetString("cmu-fighter-speed", ("speed", MathF.Round(_selectedSpeed, 1)));
        NearStall.ToolTip = Loc.GetString("cmu-fighter-speed", ("speed", aircraft.MinimumSpeed));
        Cruise.ToolTip = Loc.GetString("cmu-fighter-speed", ("speed", cruise));
        Afterburner.ToolTip = Loc.GetString("cmu-fighter-speed", ("speed", aircraft.MaximumSpeed));
    }
}
