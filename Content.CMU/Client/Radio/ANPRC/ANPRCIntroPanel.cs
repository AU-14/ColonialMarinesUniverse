using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Radio.ANPRC;

/// <summary>
///     The briefing an operator gets the first time they open a set. Short on purpose: enough to
///     get on the air and to know where everything else lives, with the guidebook one press away
///     for the rest. The ? button brings it back.
/// </summary>
public sealed class ANPRCIntroPanel : BoxContainer
{
    public event Action? OnDismissed;
    public event Action? OnOpenGuide;

    public readonly ANPRCMoreBelow MoreBelow;

    public ANPRCIntroPanel()
    {
        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = 6;

        var scroll = new ScrollContainer
        {
            VerticalExpand = true,
            HScrollEnabled = false,
            MinHeight = 120,
        };

        var column = ANPRCUi.Column(8, 8);
        scroll.AddChild(column);

        column.AddChild(ANPRCUi.Label(Loc.GetString("anprc-op-intro-title"), ANPRCUi.Good, ANPRCUi.Mono(14, true)));
        column.AddChild(ANPRCUi.Wrapped(Loc.GetString("anprc-op-intro-lead"), ANPRCUi.Text));

        for (var i = 1; i <= 5; i++)
        {
            var point = ANPRCUi.Column(1);
            point.AddChild(ANPRCUi.Label(Loc.GetString($"anprc-op-intro-point-{i}-title"), ANPRCUi.HeadingText, ANPRCUi.Mono(11, true)));
            point.AddChild(ANPRCUi.Wrapped(Loc.GetString($"anprc-op-intro-point-{i}"), ANPRCUi.Text));
            column.AddChild(point);
        }

        column.AddChild(ANPRCUi.Wrapped(Loc.GetString("anprc-op-intro-reopen"), ANPRCUi.TextDim));

        var frame = ANPRCUi.Framed(scroll, ANPRCUi.Panel, ANPRCUi.Good);
        frame.VerticalExpand = true;
        AddChild(frame);

        MoreBelow = new ANPRCMoreBelow(scroll, column);
        AddChild(MoreBelow);

        var buttons = ANPRCUi.Row(6);

        var dismiss = ANPRCUi.Button(Loc.GetString("anprc-op-intro-dismiss"), null, () => OnDismissed?.Invoke());
        dismiss.HorizontalExpand = true;

        var guide = ANPRCUi.Button(Loc.GetString("anprc-op-intro-guide"), Loc.GetString("anprc-op-intro-guide-tooltip"),
            () => OnOpenGuide?.Invoke());
        guide.HorizontalExpand = true;

        buttons.AddChild(dismiss);
        buttons.AddChild(guide);
        AddChild(buttons);
    }
}
