using System.Numerics;
using Content.Shared.CMU14.Vehicle.Jeep;
using Content.Shared.Crayon;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// Free drawing with a crayon: pressed on a vehicle, the click is kept from the interaction system and
/// the cursor's path is sent to the server in batches until it is let go.
/// </summary>
public sealed class CMUCrayonFreeDrawSystem : EntitySystem
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan SendEvery = TimeSpan.FromSeconds(0.1);
    private const float MinStep = 1f / 64f;

    private EntityUid? _vehicle;
    private MapId _map;
    private readonly List<Vector2> _points = new();
    private Vector2? _last;
    private bool _start;
    private TimeSpan _nextSend;

    public override void Initialize()
    {
        CommandBinds.Builder
            .BindBefore(EngineKeyFunctions.Use,
                new PointerInputCmdHandler(OnUse, ignoreUp: false, outsidePrediction: true),
                typeof(SharedInteractionSystem))
            .Register<CMUCrayonFreeDrawSystem>();
    }

    public override void Shutdown()
    {
        CommandBinds.Unregister<CMUCrayonFreeDrawSystem>();
    }

    private bool OnUse(in PointerInputCmdHandler.PointerInputCmdArgs args)
    {
        if (args.State == BoundKeyState.Up)
        {
            if (_vehicle == null)
                return false;

            Sample();
            Stop();
            return true;
        }

        if (args.State != BoundKeyState.Down ||
            _player.LocalEntity is not { } user ||
            !Drawing(user) ||
            VehicleOf(args.EntityUid) is not { } vehicle ||
            !_interaction.InRangeUnobstructed(user, vehicle))
        {
            return false;
        }

        _vehicle = vehicle;
        _map = Transform(vehicle).MapID;
        _start = true;
        _last = null;
        _points.Clear();
        Sample();
        Send();
        return true;
    }

    public override void FrameUpdate(float frameTime)
    {
        if (_vehicle is not { } vehicle)
            return;

        if (_player.LocalEntity is not { } user ||
            !Drawing(user) ||
            TerminatingOrDeleted(vehicle))
        {
            Stop();
            return;
        }

        Sample();
        if (_timing.RealTime >= _nextSend)
            Send();
    }

    /// <summary>
    /// Whether the user holds a crayon set to draw freely.
    /// </summary>
    private bool Drawing(EntityUid user)
    {
        return _hands.GetActiveItem(user) is { } held &&
               HasComp<CrayonComponent>(held) &&
               HasComp<CMUCrayonFreeDrawComponent>(held);
    }

    /// <summary>
    /// The vehicle a clicked entity belongs to: the vehicle, one of its parts or seats, or its overlay.
    /// </summary>
    private EntityUid? VehicleOf(EntityUid clicked)
    {
        if (!clicked.IsValid())
            return null;

        if (HasComp<CMUVehicleCrayonComponent>(clicked))
            return clicked;

        if (CompOrNull<CMUVehiclePartComponent>(clicked)?.Vehicle is { } vehicle && HasComp<CMUVehicleCrayonComponent>(vehicle))
            return vehicle;

        var parent = Transform(clicked).ParentUid;
        return HasComp<CMUVehicleCrayonComponent>(parent) ? parent : null;
    }

    private void Sample()
    {
        var mouse = _eye.PixelToMap(_input.MouseScreenPosition);
        if (mouse.MapId != _map)
            return;

        if (_last is { } last && Vector2.Distance(last, mouse.Position) < MinStep)
            return;

        _last = mouse.Position;
        _points.Add(mouse.Position);
        if (_points.Count >= SharedCMUVehicleCrayonSystem.MaxStrokePoints)
            Send();
    }

    /// <summary>
    /// Sends the points gathered since the last batch, the next batch starting from its last point.
    /// </summary>
    private void Send()
    {
        _nextSend = _timing.RealTime + SendEvery;
        if (_vehicle is not { } vehicle || _points.Count == 0 || _points.Count == 1 && !_start)
            return;

        RaiseNetworkEvent(new CMUCrayonStrokeEvent(GetNetEntity(vehicle), new List<Vector2>(_points), _start));
        var carry = _points[^1];
        _points.Clear();
        _points.Add(carry);
        _start = false;
    }

    private void Stop()
    {
        Send();
        _vehicle = null;
        _points.Clear();
        _last = null;
    }
}
