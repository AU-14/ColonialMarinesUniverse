using System.Globalization;
using Robust.Shared.Utility;

namespace Content.Client.Actions.UI;

public sealed partial class ActionAlertTooltip
{
    private readonly ILocalizationManager _cmuLocalization = IoCManager.Resolve<ILocalizationManager>();
    private (int Duration, int Remaining, CultureInfo? Culture)? _cmuCooldownText;
    private string? _cmuDynamicText;

    private void CMUUpdateText()
    {
        if (Cooldown is { } cooldown && cooldown.End > _gameTiming.CurTime)
        {
            var values = ((int) (cooldown.End - cooldown.Start).TotalSeconds,
                (int) (cooldown.End - _gameTiming.CurTime).TotalSeconds + 1,
                _cmuLocalization.DefaultCulture);
            if (_cmuCooldownText != values)
            {
                _cmuCooldownText = values;
                _cooldownLabel.Visible = FormattedMessage.TryFromMarkup(
                    _cmuLocalization.GetString("ui-actionslot-duration", ("duration", values.Item1), ("timeLeft", values.Item2)),
                    out var markup);
                if (_cooldownLabel.Visible)
                    _cooldownLabel.SetMessage(markup!);
            }
        }
        else
        {
            _cmuCooldownText = null;
            _cooldownLabel.Visible = false;
        }

        if (_cmuDynamicText == DynamicMessage)
            return;

        _cmuDynamicText = DynamicMessage;
        _dynamicMessageLabel.Visible = false;
        if (!string.IsNullOrWhiteSpace(DynamicMessage) &&
            FormattedMessage.TryFromMarkup($"[color=#ffffff]{DynamicMessage}[/color]", out var dynamicMarkup))
        {
            _dynamicMessageLabel.SetMessage(dynamicMarkup);
            _dynamicMessageLabel.Visible = true;
        }
    }
}
