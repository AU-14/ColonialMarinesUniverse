using System.Numerics;
using System.Text;
using Content.Client.CMU14.Storage; // CMU14: control-owned label cache.
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.IconLabel;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Configuration;
using Robust.Shared.Timing; // CMU14: bounded localization refresh.

namespace Content.Client._RMC14.Storage;

public sealed partial class RMCIconLabelSystem : SharedRMCIconLabelSystem
{
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private IConfigurationManager _config = default!;
    // CMU14 Begin: label presentation inputs.
    [Dependency] private IGameTiming _cmuLabelTiming = default!;
    [Dependency] private ILocalizationManager _cmuLabelLocalization = default!;
    // CMU14 End

    private Font _font = default!;

    private bool _drawStorageIconLabels;

    public override void Initialize()
    {
        base.Initialize();

        _font = new VectorFont(_cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 8);

        Subs.CVar(_config, RMCCVars.RMCDrawStorageIconLabels, v => _drawStorageIconLabels = v, true);
    }

    // CMU14 method: cache label preparation and enumerate the string without a character array.
    public void DrawStorage(EntityUid entity, float uiScale, Vector2 iconPosition, DrawingHandleScreen handle,
        ref CMUIconLabelPresentation? presentation)
    {
        if (!_drawStorageIconLabels)
            return;

        if (!TryComp(entity, out IconLabelComponent? iconLabel) || iconLabel.LabelTextLocId == null)
            return;

        var scale = 2 * uiScale;
        presentation ??= new CMUIconLabelPresentation();
        if (!presentation.TryGet(iconLabel, _cmuLabelLocalization, _cmuLabelTiming.RealTime,
                out var msg, out var textColor))
        {
            return;
        }

        var iconLabelPosition = new Vector2(iconPosition.X + scale * iconLabel.StoredOffset.X,
            iconPosition.Y + scale * iconLabel.StoredOffset.Y);

        var textSize = iconLabel.TextSize;

        float sep = 0;
        foreach (var chr in msg)
        {
            iconLabelPosition.X += sep;
            sep = _font.DrawChar(handle, new Rune(chr), iconLabelPosition, textSize * scale, textColor);
        }
    }
}
