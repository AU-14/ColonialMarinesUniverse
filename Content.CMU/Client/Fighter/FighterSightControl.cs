using System.Numerics;
using Content.Client.Viewport;
using Content.Shared.CMU14.Fighter;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Fighter;

/// <summary>Acquisition brackets and lock progress without softening or covering the sensor image.</summary>
public sealed class FighterSightControl(ScalingViewport viewport) : Control
{
    private readonly IGameTiming _timing = IoCManager.Resolve<IGameTiming>();
    private FighterAircraftComponent? _aircraft;
    private FighterSeatComponent? _seat;
    private TimeSpan _acquiredAt;
    private TimeSpan _modeAt;
    private TimeSpan _lockDuration;
    private NetEntity? _target;
    private bool _locked;
    private bool _available;
    private FighterSensorMode _mode;

    public void SetFlight(FighterAircraftComponent aircraft, FighterSeatComponent seat, FighterWeaponsComponent? weapons, bool available, TimeSpan now)
    {
        MouseFilter = MouseFilterMode.Ignore;
        RectClipContent = true;
        _aircraft = aircraft;
        _seat = seat;
        _available = available;
        _lockDuration = weapons?.LaserLockDuration ?? TimeSpan.FromSeconds(2);
        if (_target != seat.Target || _locked != (seat.SensorLock != null)) _acquiredAt = now;
        _target = seat.Target;
        _locked = seat.SensorLock != null;
        var mode = FighterOptics.Mode(aircraft, now);
        if (_mode != mode) _modeAt = now;
        _mode = mode;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (_aircraft is not { } aircraft || _seat is not { } seat) return;
        var now = _timing.CurTime;
        var age = (float) (now - _acquiredAt).TotalSeconds;
        var point = FighterFlight.SensorPosition(aircraft, seat);
        var center = viewport.WorldToScreen(point) - GlobalPixelPosition;
        var clear = _available && !FighterOptics.CloudsBlock(aircraft, point, now);
        var color = clear ? Color.FromHex("#BFEBA3") : Color.FromHex("#E3AD60");
        var scale = Math.Max(1, UIScale);
        if ((_locked || _target != null) && _available)
        {
            var radius = (15 + Math.Max(0, 1 - age / .45f) * 24) * scale;
            for (var x = -1; x <= 1; x += 2)
            for (var y = -1; y <= 1; y += 2)
            {
                var corner = center + new Vector2(x, y) * radius;
                handle.DrawLine(corner, corner - new Vector2(x * 7, 0) * scale, color);
                handle.DrawLine(corner, corner - new Vector2(0, y * 7) * scale, color);
            }
        }
        if (seat.LaserLockTarget != null)
        {
            var progress = Math.Clamp(1 - (float) ((seat.LaserLockReadyAt - now).TotalSeconds / Math.Max(.01, _lockDuration.TotalSeconds)), 0, 1);
            for (var i = 0; i < (int) (progress * 48); i++)
            {
                var angle = -MathF.PI / 2 + i * MathF.Tau / 48;
                var next = angle + MathF.Tau / 48;
                handle.DrawLine(center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 28 * scale,
                    center + new Vector2(MathF.Cos(next), MathF.Sin(next)) * 28 * scale, Color.FromHex("#FFBB64"));
            }
        }
        var modeAge = (float) (now - _modeAt).TotalSeconds;
        if (_modeAt != TimeSpan.Zero && modeAge < .35f)
            handle.DrawRect(PixelSizeBox, color.WithAlpha((1 - modeAge / .35f) * .65f), false);
    }
}
