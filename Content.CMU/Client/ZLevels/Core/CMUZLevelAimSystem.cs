using Content.Client.Gameplay;
using Content.Client.Weapons.Ranged.Systems;
using Content.Shared.CMU14.ZLevels;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Physics;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.ZLevels.Core;

// shoot where you look. figures out which level the cursor is actually showing and keeps
// the aim-down flag synced, so there's no hotkey for firing down a hole anymore
public sealed partial class CMUZLevelAimSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IStateManager _state = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private CMUClientZLevelsSystem _zLevels = default!;
    [Dependency] private CMUZLevelShootingSystem _shooting = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        // the gun reads the flag in the same tick it fires, so it has to be fresh by then
        UpdatesBefore.Add(typeof(GunSystem));
    }

    public override void Update(float frameTime)
    {
        if (!_timing.IsFirstTimePredicted ||
            _player.LocalEntity is not { } user)
        {
            return;
        }

        var aimDown = _input.MouseScreenPosition.IsValid &&
            ResolveAim(user, _eye.PixelToMap(_input.MouseScreenPosition), out _) < 0;

        if (aimDown != _shooting.IsAimingDown(user))
            RaisePredictiveEvent(new CMUZLevelSetAimDownEvent(aimDown));
    }

    // other levels are drawn shifted, so the raw cursor spot is off by ~3/4 tile.
    // aim at what's drawn there instead, snapping onto a sprite if there is one
    public bool TryGetCrossZAim(EntityUid user, MapCoordinates cursor, out MapCoordinates aim)
    {
        var level = ResolveAim(user, cursor, out aim);
        if (level != 0 && _shooting.GetShotOffset(user) == level)
            return true;

        aim = cursor;
        return false;
    }

    // 1 looking up, -1 through a hole with nothing on our level drawn over it, else 0
    private int ResolveAim(EntityUid user, MapCoordinates cursor, out MapCoordinates aim)
    {
        aim = cursor;

        if (!_config.GetCVar(CMUZLevelsCVars.Enabled))
            return 0;

        var xform = Transform(user);
        if (xform.MapUid is not { } map ||
            cursor.MapId != xform.MapID)
        {
            return 0;
        }

        // looking up draws the level above over everything, same as the old look-up shots
        if (TryComp<CMUZLevelViewerComponent>(user, out var viewer) && viewer.LookUp)
            return TryAimAtLevel(user, map, 1, cursor, out aim) ? 1 : 0;

        if (_config.GetCVar(CMUZLevelsCVars.MaxRenderDepth) <= 0 ||
            !_zLevels.IsZShotPathOpen(map, cursor.Position, cursor.Position) ||
            GetShootableUnderCursor(user, cursor) != null)
        {
            return 0;
        }

        return TryAimAtLevel(user, map, -1, cursor, out aim) ? -1 : 0;
    }

    private bool TryAimAtLevel(EntityUid user, EntityUid map, int offset, MapCoordinates cursor, out MapCoordinates aim)
    {
        aim = cursor;
        if (!_zLevels.TryMapOffset(map, offset, out _, out var levelMap))
            return false;

        // same per-depth eye shift the viewport renders that level with
        Angle rotation = _eye.CurrentEye.Rotation * -1;
        var position = cursor.Position + rotation.ToWorldVec() * CMUClientZLevelsSystem.ZLevelOffset * offset;
        aim = new MapCoordinates(position, cursor.MapId);

        if (GetShootableUnderCursor(user, new MapCoordinates(position, levelMap.MapId)) is { } target)
            aim = new MapCoordinates(_transform.GetWorldPosition(target), cursor.MapId);

        return true;
    }

    private EntityUid? GetShootableUnderCursor(EntityUid user, MapCoordinates coordinates)
    {
        if (_state.CurrentState is not GameplayStateBase screen)
            return null;

        foreach (var uid in screen.GetClickableEntities(coordinates, _eye.CurrentEye))
        {
            if (uid != user && IsShootable(uid))
                return uid;
        }

        return null;
    }

    // matches the gun's own click-target check
    private bool IsShootable(EntityUid uid)
    {
        if (!TryComp<PhysicsComponent>(uid, out var body) ||
            !body.CanCollide ||
            !TryComp<FixturesComponent>(uid, out var fixtures))
        {
            return false;
        }

        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (fixture.Hard && (fixture.CollisionLayer & (int) CollisionGroup.BulletImpassable) != 0)
                return true;
        }

        return false;
    }
}
