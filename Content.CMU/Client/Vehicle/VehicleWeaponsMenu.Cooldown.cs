using System.Globalization;
using Robust.Client.Graphics;

namespace Content.Client._RMC14.Vehicle.Ui;

public sealed partial class VehicleWeaponsMenu
{
    private static readonly Color CMUCooldownFlashColor = Color.FromHex("#E1786A");
    private static readonly Color CMUCooldownChargingColor = Color.FromHex("#D6C45A");
    private static readonly Color CMUCooldownReadyColor = Color.FromHex("#53D188");
    private static readonly Color CMUCooldownAmmoColor = Color.FromHex("#C7D9ED");
    private readonly StyleBoxFlat _cmuCooldownForeground = new();
    private readonly StyleBoxFlat _cmuCooldownBackground = new() { BackgroundColor = Color.FromHex("#1E2B3B") };
    private readonly ILocalizationManager _cmuLocalization = IoCManager.Resolve<ILocalizationManager>();
    private (bool Charging, string? Seconds, CultureInfo? Culture)? _cmuDisplayedCooldown;
    private string _cmuCooldownText = string.Empty;

    private void CMUUpdateCooldownText(float remaining)
    {
        var charging = remaining > 0.001f;
        string? seconds = null;
        if (charging)
        {
            Span<char> buffer = stackalloc char[64];
            if (!remaining.TryFormat(buffer, out var length, "0.0"))
                seconds = remaining.ToString("0.0");
            else
            {
                var previous = _cmuDisplayedCooldown?.Seconds;
                seconds = buffer[..length].SequenceEqual(previous.AsSpan()) ? previous : new string(buffer[..length]);
            }
        }

        var displayed = (charging, seconds, _cmuLocalization.DefaultCulture);
        if (_cmuDisplayedCooldown != displayed)
        {
            _cmuDisplayedCooldown = displayed;
            _cmuCooldownText = charging
                ? Loc.GetString("rmc-vehicle-weapons-ui-cooldown-recharging", ("seconds", seconds!))
                : Loc.GetString("rmc-vehicle-weapons-ui-cooldown-ready");
        }

        // Selection changes can clear the label even when the next weapon has the same countdown.
        CooldownLabel.Text = _cmuCooldownText;
    }
}
