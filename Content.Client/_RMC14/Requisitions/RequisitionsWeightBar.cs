using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._RMC14.Requisitions;

public sealed class RequisitionsWeightBar : ProgressBar
{
    private float _target;
    private RequisitionsTerminalTheme _theme = RequisitionsTerminalTheme.Manifest;

    public RequisitionsWeightBar()
    {
        MinHeight = 16;
    }

    public void SetLoad(int weight, int limit, RequisitionsTerminalTheme theme, bool animate = true)
    {
        _theme = theme;
        MaxValue = Math.Max(1, limit);
        _target = Math.Clamp(weight, 0, (int) MaxValue);
        if (!animate)
            Value = _target;
        UpdateStyle();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (MathF.Abs(Value - _target) < 0.02f)
        {
            Value = _target;
            return;
        }

        Value += (_target - Value) * MathF.Min(1f, args.DeltaSeconds * 9f);
        UpdateStyle();
    }

    private void UpdateStyle()
    {
        var ratio = MaxValue <= 0 ? 0 : Value / MaxValue;
        var color = ratio >= 0.999f ? _theme.Alert : ratio >= 0.8f ? _theme.Caution : _theme.Accent;
        BackgroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = _theme.Background };
        ForegroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = color };
    }
}
