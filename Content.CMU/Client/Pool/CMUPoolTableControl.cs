using System.Numerics;
using Content.Shared.CMU14.Pool;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Pool;

internal sealed class CMUPoolTableControl : Control
{
    private const int Rail = 28;
    private const float MaxPull = 118;
    private static readonly Color[] BallColors =
    [
        Color.FromHex("#F4E9D1"),
        Color.FromHex("#DFB13A"), Color.FromHex("#4A86D2"), Color.FromHex("#D34A42"),
        Color.FromHex("#8053AC"), Color.FromHex("#E47738"), Color.FromHex("#4D9957"),
        Color.FromHex("#914640"), Color.FromHex("#22252A"),
    ];

    private readonly Texture _feltDetails;
    private Vector2 _aim = Vector2.UnitX;
    private Vector2 _pointer = new(-100, -100);
    private Vector2 _pullStart;
    private bool _pulling;
    private float _power;
    private int _revision;
    private Vector2[]? _target;
    private readonly Func<CMUPoolVisualSystem.View?> _readView;
    private readonly Action<CMUPoolMessage> _send;
    public bool WaitingForTable => _target == null;
    private bool IsPlacingCueBall => !WaitingForTable && !Game.Rolling && Game.CueBall.Pocketed && Game.Remaining > 0;

    internal CMUPoolSimulation Game { get; } = new();
    public event Action? StateChanged;
    public event Action<float>? PowerChanged;

    internal CMUPoolTableControl(Func<CMUPoolVisualSystem.View?> readView, Action<CMUPoolMessage> send)
    {
        _feltDetails = IoCManager.Resolve<IResourceCache>().GetResource<TextureResource>(
            "/Textures/CMU14/Interface/Pool/table_felt_details.png").Texture;
        _readView = readView;
        _send = send;
        MinSize = new Vector2(CMUPoolSimulation.Width + 2 * Rail, CMUPoolSimulation.Height + 2 * Rail);
        MouseFilter = MouseFilterMode.Stop;
        RectClipContent = true;
    }

    public void Rack()
    {
        if (!WaitingForTable && !Game.Rolling)
            _send(new CMUPoolMessage(CMUPoolAction.Rack, _revision, Vector2.Zero));
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        TrackPointer(args.RelativePosition);
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        if (!_pulling)
            _pointer = new Vector2(-100, -100);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        args.Handle();
        TrackPointer(args.RelativePosition);
        if (WaitingForTable || Game.Rolling || Game.Remaining == 0 || _pulling)
            return;

        if (Game.CueBall.Pocketed)
        {
            if (!IsPlacingCueBall || !Game.CanPlaceCueBall(_pointer))
                return;

            _send(new CMUPoolMessage(CMUPoolAction.PlaceCueBall, _revision, _pointer));
            return;
        }

        _pullStart = _pointer;
        _pulling = true;
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function != EngineKeyFunctions.UIClick || !_pulling)
            return;

        args.Handle();
        TrackPointer(args.RelativePosition);
        _pulling = false;
        if (!WaitingForTable && _power >= CMUPoolSimulation.MinimumShotPower)
            _send(new CMUPoolMessage(CMUPoolAction.Shoot, _revision, _aim, _power));
        SetPower(0);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        var view = _readView();
        if (view == null)
        {
            if (_target == null)
                return;

            _target = null;
            _pulling = false;
            SetPower(0);
            StateChanged?.Invoke();
            return;
        }

        if (!view.Rolling && ReferenceEquals(_target, view.Target))
            return;

        var changed = _target == null || _revision != view.Revision || Game.Rolling != view.Rolling;
        var previousRemaining = Game.Remaining;
        _target = view.Target;
        _revision = view.Revision;
        Game.ApplyPresentation(view.Positions, view.PocketedMask, view.Rolling);
        if (Game.Rolling && _pulling)
        {
            _pulling = false;
            SetPower(0);
        }

        if (changed || previousRemaining != Game.Remaining)
            StateChanged?.Invoke();
    }

    private void TrackPointer(Vector2 local)
    {
        _pointer = local - new Vector2(Rail);
        if (_pulling)
        {
            SetPower(Math.Clamp(Vector2.Dot(_pullStart - _pointer, _aim) / MaxPull, 0, 1));
            return;
        }

        var offset = _pointer - Game.CueBall.Position;
        if (!Game.Rolling && !Game.CueBall.Pocketed && offset.LengthSquared() > 4)
            _aim = Vector2.Normalize(offset);
    }

    private void SetPower(float power)
    {
        _power = power;
        PowerChanged?.Invoke(power);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var scale = UIScale;
        const int width = CMUPoolSimulation.Width;
        const int height = CMUPoolSimulation.Height;
        DrawTableRect(handle, -Rail, -Rail, width + 2 * Rail, height + 2 * Rail, Color.FromHex("#232021"));
        DrawTableRect(handle, -25, -24, width + 50, height + 48, Color.FromHex("#58392E"));
        DrawTableRect(handle, -23, -22, width + 46, height + 44, Color.FromHex("#77503A"));
        DrawTableRect(handle, -18, -17, width + 36, height + 34, Color.FromHex("#493128"));
        DrawTableRect(handle, -11, -11, width + 22, height + 22, Color.FromHex("#244A3D"));
        DrawTableRect(handle, 0, 0, width, height, Color.FromHex("#347059"));
        DrawTableRect(handle, 2, 2, width - 4, height - 4, Color.FromHex("#326C55"));

        for (var i = 1; i < 8; i++)
        {
            if (i != 4)
            {
                Circle(handle, new Vector2(i * width / 8f, -20), 1.6f, Color.FromHex("#D4B791"));
                Circle(handle, new Vector2(i * width / 8f, height + 20), 1.6f, Color.FromHex("#D4B791"));
            }
        }
        for (var i = 1; i < 4; i++)
        {
            Circle(handle, new Vector2(-20, i * height / 4f), 1.6f, Color.FromHex("#D4B791"));
            Circle(handle, new Vector2(width + 20, i * height / 4f), 1.6f, Color.FromHex("#D4B791"));
        }
        handle.DrawTextureRect(_feltDetails, UIBox2.FromDimensions(0, 0,
            (width + 2 * Rail) * scale, (height + 2 * Rail) * scale));

        if (WaitingForTable)
            return;

        if (IsPlacingCueBall)
            DrawPlacementBoundary(handle);
        else if (!Game.Rolling && !Game.CueBall.Pocketed && Game.Remaining > 0)
            DrawAim(handle);

        for (var i = 0; i < Game.Balls.Length; i++)
        {
            var ball = Game.Balls[i];
            if (ball.Pocketed)
                continue;

            var color = BallColors[i > 8 ? i - 8 : i];
            var radius = CMUPoolSimulation.Radius;
            Circle(handle, ball.Position + new Vector2(1.4f, 2), radius + 1, Color.FromHex("#1C392D"));
            Circle(handle, ball.Position, radius, i > 8 ? BallColors[0] : color);
            if (i > 8)
            {
                for (var stripeRow = -3; stripeRow <= 3; stripeRow++)
                {
                    var halfWidth = MathF.Sqrt(radius * radius - stripeRow * stripeRow) - 0.5f;
                    DrawTableRect(handle, ball.Position.X - halfWidth, ball.Position.Y + stripeRow - 0.5f,
                        halfWidth * 2, 1, color);
                }
            }
            if (i != 0)
                Circle(handle, ball.Position, i == 8 ? 3.3f : 2.4f, BallColors[0]);
            if (i == 8)
                Circle(handle, ball.Position, 1.5f, BallColors[8]);
            Circle(handle, ball.Position - new Vector2(2.4f, 3.1f), 1.4f, Color.White.WithAlpha(0.75f));
        }

        if (IsPlacingCueBall && Game.CanPlaceCueBall(_pointer))
        {
            var color = Color.FromHex("#CDE9CB");
            Circle(handle, _pointer, CMUPoolSimulation.Radius, color.WithAlpha(0.36f));
            handle.DrawCircle(ToPixel(_pointer), (CMUPoolSimulation.Radius + 1) * scale, color, filled: false);
            handle.DrawLine(ToPixel(_pointer - new Vector2(3, 0)), ToPixel(_pointer + new Vector2(3, 0)), color);
            handle.DrawLine(ToPixel(_pointer - new Vector2(0, 3)), ToPixel(_pointer + new Vector2(0, 3)), color);
        }

        void DrawAim(DrawingHandleScreen drawing)
        {
            var cueBall = Game.CueBall.Position;
            var guideLength = 290f;
            const float diameter = 2 * CMUPoolSimulation.Radius;
            for (var i = 1; i < Game.Balls.Length; i++)
            {
                var target = Game.Balls[i];
                if (target.Pocketed)
                    continue;

                var offset = target.Position - cueBall;
                var forward = Vector2.Dot(offset, _aim);
                var perpendicularSquared = offset.LengthSquared() - forward * forward;
                if (forward > 0 && perpendicularSquared <= diameter * diameter)
                    guideLength = Math.Min(guideLength, forward - MathF.Sqrt(diameter * diameter - perpendicularSquared));
            }

            for (var i = 18; i <= guideLength; i += 9)
            {
                var dot = cueBall + _aim * i;
                if (dot.X < 3 || dot.Y < 3 || dot.X > width - 3 || dot.Y > height - 3)
                    break;
                Circle(drawing, dot, 1.55f, Color.FromHex("#F5E5B6").WithAlpha(0.84f));
            }

            var tip = cueBall - _aim * (13 + 44 * _power);
            var butt = tip - _aim * 114;
            var perpendicular = new Vector2(-_aim.Y, _aim.X);
            for (var i = -2; i <= 2; i++)
            {
                var offset = perpendicular * i;
                drawing.DrawLine(ToPixel(butt + offset), ToPixel(tip + offset),
                    i == 0 ? Color.FromHex("#DBC193") : Color.FromHex("#744A34"));
            }
            drawing.DrawLine(ToPixel(butt), ToPixel(Vector2.Lerp(butt, tip, 0.4f)), Color.FromHex("#263C37"));
            Circle(drawing, tip, 2, Color.FromHex("#86B9BF"));
        }

        Vector2 ToPixel(Vector2 local) => (local + new Vector2(Rail)) * scale;
    }

    private void DrawPlacementBoundary(DrawingHandleScreen handle)
    {
        const int width = CMUPoolSimulation.Width;
        const int height = CMUPoolSimulation.Height;
        const float radius = CMUPoolSimulation.Radius;
        var guide = Color.FromHex("#A2D4B2").WithAlpha(0.82f);
        var scale = UIScale;
        var previousX = radius;
        var previousInset = PlacementInset(previousX);
        for (var x = radius + 2; x <= width - radius; x += 2)
        {
            var inset = PlacementInset(x);
            Segment(previousX, previousInset, x, inset);
            Segment(previousX, height - previousInset, x, height - inset);
            previousX = x;
            previousInset = inset;
        }

        var sideInset = PlacementInset(radius);
        Segment(radius, sideInset, radius, height - sideInset);
        Segment(width - radius, sideInset, width - radius, height - sideInset);

        void Segment(float x1, float y1, float x2, float y2)
        {
            handle.DrawLine(new Vector2(x1 + Rail, y1 + Rail) * scale,
                new Vector2(x2 + Rail, y2 + Rail) * scale, guide);
        }
    }

    private static float PlacementInset(float x)
    {
        const int width = CMUPoolSimulation.Width;
        const float clearance = CMUPoolSimulation.PocketRadius + CMUPoolSimulation.Radius;
        var distance = MathF.Min(MathF.Min(x, width - x), MathF.Abs(x - width / 2f));
        return MathF.Max(CMUPoolSimulation.Radius,
            MathF.Sqrt(MathF.Max(0, clearance * clearance - distance * distance)));
    }

    private void DrawTableRect(DrawingHandleScreen handle, float x, float y, float width, float height, Color color)
        => handle.DrawRect(UIBox2.FromDimensions((x + Rail) * UIScale, (y + Rail) * UIScale,
            width * UIScale, height * UIScale), color);

    private void Circle(DrawingHandleScreen handle, Vector2 center, float radius, Color color)
        => handle.DrawCircle((center + new Vector2(Rail)) * UIScale, radius * UIScale, color);
}
