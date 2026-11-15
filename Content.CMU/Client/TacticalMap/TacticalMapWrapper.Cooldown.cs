using System.Globalization;

namespace Content.Client._RMC14.TacticalMap;

public sealed partial class TacticalMapWrapper
{
    private readonly ILocalizationManager _cmuLocalization = IoCManager.Resolve<ILocalizationManager>();
    private (int Seconds, CultureInfo? Culture)? _cmuDisplayedCooldown;

    private void CMUUpdateCooldownText(int seconds)
    {
        var displayed = (seconds, _cmuLocalization.DefaultCulture);
        if (_cmuDisplayedCooldown == displayed)
            return;

        _cmuDisplayedCooldown = displayed;
        CooldownLabel.Text = Loc.GetString("ui-tactical-map-cooldown-seconds", ("seconds", seconds));
    }
}
