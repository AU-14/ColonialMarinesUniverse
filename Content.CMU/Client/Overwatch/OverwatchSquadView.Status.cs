using System.Globalization;

namespace Content.Client._RMC14.Overwatch;

public sealed partial class OverwatchSquadView
{
    private readonly ILocalizationManager _cmuStatusLocalization = IoCManager.Resolve<ILocalizationManager>();
    private (bool Charging, bool Ready, int Seconds, CultureInfo? Culture)? _cmuSupplyStatus;
    private (bool Charging, bool Ready, int Seconds, CultureInfo? Culture)? _cmuOrbitalStatus;

    private void CMUUpdateSupplyStatus(TimeSpan remaining)
    {
        var charging = remaining > TimeSpan.Zero;
        var seconds = charging ? (int) remaining.TotalSeconds : 0;
        var displayed = (charging, !charging && HasCrate, seconds, _cmuStatusLocalization.DefaultCulture);
        if (_cmuSupplyStatus == displayed)
            return;

        _cmuSupplyStatus = displayed;
        CrateStatus.Text = charging
            ? Loc.GetString("rmc-overwatch-console-cooldown", ("seconds", seconds))
            : HasCrate
                ? "[color=green][bold] \\[ CRATE LOADED \\][/bold][/color]"
                : "[color=red][bold] \\[ NO CRATE LOADED \\][/bold][/color]";
    }

    private void CMUUpdateOrbitalStatus(TimeSpan remaining)
    {
        var charging = remaining > TimeSpan.Zero;
        var seconds = charging ? (int) remaining.TotalSeconds : 0;
        var displayed = (charging, !charging && HasOrbital, seconds, _cmuStatusLocalization.DefaultCulture);
        if (_cmuOrbitalStatus == displayed)
            return;

        _cmuOrbitalStatus = displayed;
        OrbitalStatus.Text = charging
            ? Loc.GetString("rmc-overwatch-console-cooldown", ("seconds", seconds))
            : HasOrbital
                ? "[color=green][bold] \\[ READY \\][/bold][/color]"
                : "[color=red][bold] \\[ NOT READY \\][/bold][/color]";
    }
}
