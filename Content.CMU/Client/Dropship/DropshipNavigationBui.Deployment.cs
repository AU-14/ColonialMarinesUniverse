using Content.Shared._RMC14.Dropship;

namespace Content.Client._RMC14.Dropship;

public sealed partial class DropshipNavigationBui
{
    private int _displayedDepartureSeconds;

    private void UpdateOpeningDeparture()
    {
        if (_window == null)
            return;

        var seconds = _entities.TryGetComponent<DropshipNavigationComputerComponent>(Owner, out var console) &&
                      console.OpeningDeparture is { } deadline
            ? Math.Max(0, (int) Math.Ceiling((deadline - _timing.CurTime).TotalSeconds))
            : 0;
        if (seconds == _displayedDepartureSeconds)
            return;

        _displayedDepartureSeconds = seconds;
        var label = _window.OpeningDepartureLabel;
        label.Visible = seconds > 0;
        if (label.Visible)
            label.Text = Loc.GetString("cmu-fof-launch-countdown", ("time", TimeSpan.FromSeconds(seconds).ToString(@"m\:ss")));
    }
}
