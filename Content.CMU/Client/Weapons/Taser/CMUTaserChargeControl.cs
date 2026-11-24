using System.Numerics;
using Content.Client.Message;
using Content.Client.Stylesheets;
using Content.Shared.CMU14.Weapons.Taser;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Weapons.Taser;

/// <summary>A wired taser's charge bar and wire state, for the hand HUD.</summary>
public sealed class CMUTaserChargeControl : Control
{
    private static readonly Color Charged = Color.FromHex("#ffd21f");
    private static readonly Color Empty = Color.FromHex("#d81b1b");

    /// <summary>At or below this much charge the bar turns red.</summary>
    private const float RedAt = 0.3f;

    private readonly EntityUid _taser;
    private readonly IEntityManager _entities;
    private readonly SharedBatterySystem _battery;
    private readonly IGameTiming _timing;

    private readonly ProgressBar _bar;
    private readonly StyleBoxFlat _fill;
    private readonly RichTextLabel _label;

    private int _lastPercent = -1;
    private int _lastState = -1;

    public CMUTaserChargeControl(EntityUid taser, IEntityManager entities, SharedBatterySystem battery, IGameTiming timing)
    {
        _taser = taser;
        _entities = entities;
        _battery = battery;
        _timing = timing;

        MinHeight = 15;
        HorizontalExpand = true;
        VerticalAlignment = VAlignment.Center;

        _fill = new StyleBoxFlat { BackgroundColor = Charged };
        _bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            MinSize = new Vector2(60, 8),
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(0, 0, 5, 0),
            ForegroundStyleBoxOverride = _fill,
            BackgroundStyleBoxOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#14171c"),
                BorderColor = Color.FromHex("#3a4048"),
                BorderThickness = new Thickness(1),
            },
        };

        _label = new RichTextLabel
        {
            StyleClasses = { StyleNano.StyleClassItemStatus },
            VerticalAlignment = VAlignment.Center,
        };

        AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Children = { _bar, _label },
        });
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!_entities.TryGetComponent(_taser, out BatteryComponent? battery) || battery.MaxCharge <= 0)
            return;

        var level = Math.Clamp(_battery.GetCharge((_taser, battery)) / battery.MaxCharge, 0f, 1f);
        var percent = (int) MathF.Ceiling(level * 100);

        // 0: no wire out, 1: wire in someone, 2: current running.
        var state = 0;
        if (_entities.TryGetComponent(_taser, out CMUTetherTaserComponent? taser) && taser.Target != null)
            state = _timing.CurTime - taser.LastTrigger <= taser.HoldWindow ? 2 : 1;

        if (percent == _lastPercent && state == _lastState)
            return;

        _lastPercent = percent;
        _lastState = state;

        _bar.Value = level;
        // Yellow while there's charge to spare, red from 30% down.
        _fill.BackgroundColor = level <= RedAt ? Empty : Charged;

        var stateKey = state switch
        {
            2 => "cmu-taser-status-shocking",
            1 => "cmu-taser-status-wired",
            _ => "cmu-taser-status-ready",
        };

        _label.SetMarkup(Loc.GetString("cmu-taser-status", ("percent", percent), ("state", Loc.GetString(stateKey))));
    }
}
