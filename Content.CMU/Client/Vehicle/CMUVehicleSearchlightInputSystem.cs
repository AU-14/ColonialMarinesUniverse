using Content.Shared.CMU14.Vehicle.Open;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// Points the searchlight its local player is working at their cursor, from its lens.
/// </summary>
public sealed partial class CMUVehicleSearchlightInputSystem : EntitySystem
{
    private static readonly TimeSpan AimInterval = TimeSpan.FromSeconds(0.05);
    private static readonly Angle AimEpsilon = Angle.FromDegrees(0.5);

    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private CMUVehicleSearchlightSystem _searchlights = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private Angle _lastAngle;
    private TimeSpan _lastSent;

    public override void Update(float frameTime)
    {
        if (!_timing.IsFirstTimePredicted ||
            _player.LocalEntity is not { } user ||
            !TryComp(user, out CMUVehicleSearchlightOperatorComponent? operating) ||
            !TryComp(operating.Vehicle, out CMUVehicleSearchlightComponent? searchlight) ||
            searchlight.Operator != user)
        {
            return;
        }

        var mouse = _eye.PixelToMap(_input.MouseScreenPosition);
        if (mouse.MapId == MapId.Nullspace || mouse.MapId != _transform.GetMapId(operating.Vehicle))
            return;

        var direction = mouse.Position - _searchlights.LensPosition(operating.Vehicle, searchlight.Lamp);
        if (direction.LengthSquared() <= 0.0001f)
            return;

        var angle = direction.ToWorldAngle();
        var now = _timing.RealTime;
        if (now - _lastSent < AimInterval ||
            Math.Abs(Angle.ShortestDistance(angle, _lastAngle).Degrees) < AimEpsilon.Degrees)
        {
            return;
        }

        _lastAngle = angle;
        _lastSent = now;
        RaiseNetworkEvent(new CMUVehicleSearchlightAimEvent
        {
            Vehicle = GetNetEntity(operating.Vehicle),
            Angle = angle,
        });
    }
}
