using System.Threading;
using Content.Shared.CCVar;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Client.Stylesheets;

public sealed partial class StylesheetManager
{
    private CancellationTokenSource? _crtPreviewDelay;
    private string? _appliedCrtColor;

    private void RememberCrtUiSettings() => _appliedCrtColor = _configurationManager.GetCVar(CCVars.CrtUiColor);

    public void PreviewCrtUi(bool enabled, string color)
    {
        CancelCrtPreview();
        if (enabled == StyleNano.CrtUiEnabled && color == _appliedCrtColor)
            return;

        // The picker updates on every drag sample. Restyling the entire UI on each sample
        // blocks input; keep the picker responsive and apply its latest value after a short pause.
        _crtPreviewDelay = new CancellationTokenSource();
        Timer.Spawn(120, () =>
        {
            _crtPreviewDelay?.Dispose();
            _crtPreviewDelay = null;
            ApplyCrtUi(enabled, color);
        }, _crtPreviewDelay.Token);
    }

    public void ResetCrtUiPreview()
    {
        // Closing or applying must supersede a queued drag, even before the timer has fired.
        CancelCrtPreview();
        ApplyCrtUi(_configurationManager.GetCVar(CCVars.CrtUiEnabled),
            _configurationManager.GetCVar(CCVars.CrtUiColor));
    }

    private void OnCrtUiEnabledChanged(bool _) => ResetCrtUiPreview();
    private void OnCrtUiColorChanged(string _) => ResetCrtUiPreview();

    private void CancelCrtPreview()
    {
        _crtPreviewDelay?.Cancel();
        _crtPreviewDelay?.Dispose();
        _crtPreviewDelay = null;
    }

    private void ApplyCrtUi(bool enabled, string color)
    {
        if (enabled == StyleNano.CrtUiEnabled && color == _appliedCrtColor)
            return;

        _appliedCrtColor = color;
        StyleNano.SetCrtUiEnabled(enabled);
        StyleNano.SetCrtPalette(color);
        RefreshCrtTheme();
    }
}
