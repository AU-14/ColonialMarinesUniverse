using System.Numerics;
using Content.Client.CMU14.Storage;
using Content.Shared._RMC14.IconLabel;
using Robust.Shared.Timing;

namespace Content.Client.UserInterface.Controls;

public abstract partial class SlotControl
{
    [Dependency] private IGameTiming _cmuLabelTiming = default!;
    private CMUIconLabelPresentation? _cmuLabelPresentation;

    private void CMUUpdateIconLabel()
    {
        var text = "";
        var color = Color.White;
        if (_entities.TryGetComponent(Entity, out IconLabelComponent? label))
        {
            _cmuLabelPresentation ??= new CMUIconLabelPresentation();
            if (!_cmuLabelPresentation.TryGet(label, _loc, _cmuLabelTiming.RealTime, out text, out color, Color.White))
                text = "";
            var size = new Vector2(label.TextSize);
            if (IconLabel.SetSize != size)
                IconLabel.SetSize = size;
        }
        else
        {
            _cmuLabelPresentation = null;
        }

        if (IconLabel.Text != text)
            IconLabel.Text = text;
        if (IconLabel.FontColorOverride != color)
            IconLabel.FontColorOverride = color;
    }
}
