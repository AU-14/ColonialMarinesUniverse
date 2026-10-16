using System.Numerics;
using Content.Client.Gameplay;
using Content.Client.Weapons.Ranged.Systems;
using Content.Shared.CMU14.ZLevels;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Shared.Configuration;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.ZLevels.Core;

// shoot where you look. figures out which level the cursor is actually showing and keeps
// the aim-down flag synced. turning cmu.zlevels.look_aim off hands it back to the hotkey
public sealed partial class CMUZLevelAimSystem : EntitySystem
{
    // past this, empty floor through a gap doesn't pull the shot down. you have to be pointing
    // at something down there, otherwise shooting across a wide drop always went into the pit
    private const float EmptyFloorAimRange = 4f;

    // a moving target below doesn't need the cursor dead on its sprite, anyone alive this
    // close to the cursor counts. the gun does the same thing on your own level
    private const float TargetSnapRadius = 1f;

    // slipping off a target past EmptyFloorAimRange keeps you aiming down this long, and for
    // as long as the trigger is held, so a burst never flips between levels mid-spray
    private static readonly TimeSpan StickyAimDown = TimeSpan.FromSeconds(0.5);

    private TimeSpan _lastTargetBelow;

    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IStateManager _state = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private CMUClientZLevelsSystem _zLevels = default!;
    [Dependency] private CMUZLevelShootingSystem _shooting = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private GunSystem _gun = default!;
    [Dependency] private InputSystem _inputSystem = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SpriteTreeSystem _spriteTree = default!;

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

        // the hotkey has its own flag, this one is only the cursor's opinion
        var aimDown = !_shooting.IsShootDownEnabled(user) &&
            _input.MouseScreenPosition.IsValid &&
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

    // 1 looking up, -1 hotkey on or through a hole with nothing on our level drawn over it, else 0
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

        // hotkey already said down, just line the aim up with what's drawn there
        if (_shooting.IsShootDownEnabled(user))
            return TryAimAtLevel(user, map, -1, cursor, out aim, out _) ? -1 : 0;

        // looking up draws the level above over everything, same as the old look-up shots
        if (TryComp<CMUZLevelViewerComponent>(user, out var viewer) && viewer.LookUp)
            return TryAimAtLevel(user, map, 1, cursor, out aim, out _) ? 1 : 0;

        if (!_config.GetCVar(CMUZLevelsCVars.LookAim) ||
            _config.GetCVar(CMUZLevelsCVars.MaxRenderDepth) <= 0 ||
            !_zLevels.IsZShotPathOpen(map, cursor.Position, cursor.Position) ||
            GetShootableUnderCursor(user, cursor) != null ||
            !TryAimAtLevel(user, map, -1, cursor, out aim, out var snapped))
        {
            aim = cursor;
            return 0;
        }

        if (snapped)
        {
            _lastTargetBelow = _timing.RealTime;
        }
        else if ((cursor.Position - _transform.GetWorldPosition(xform)).Length() > EmptyFloorAimRange &&
                 !KeepAimingDown(user))
        {
            aim = cursor;
            return 0;
        }

        return -1;
    }

    private bool KeepAimingDown(EntityUid user)
    {
        if (!_shooting.IsAimingDown(user))
            return false;

        if (_timing.RealTime - _lastTargetBelow < StickyAimDown)
            return true;

        if (!_gun.TryGetGun(user, out var gun))
            return false;

        var useKey = gun.Comp.UseKey ? EngineKeyFunctions.Use : EngineKeyFunctions.UseSecondary;
        return _inputSystem.CmdStates.GetState(useKey) == BoundKeyState.Down;
    }

    private bool TryAimAtLevel(
        EntityUid user,
        EntityUid map,
        int offset,
        MapCoordinates cursor,
        out MapCoordinates aim,
        out bool snapped)
    {
        aim = cursor;
        snapped = false;
        if (!_zLevels.TryMapOffset(map, offset, out _, out var levelMap))
            return false;

        // same per-depth eye shift the viewport renders that level with
        Angle rotation = _eye.CurrentEye.Rotation * -1;
        var shift = rotation.ToWorldVec() * CMUClientZLevelsSystem.ZLevelOffset * offset;
        var position = cursor.Position + shift;
        aim = new MapCoordinates(position, cursor.MapId);

        var levelCursor = new MapCoordinates(position, levelMap.MapId);
        if ((GetShootableUnderCursor(user, levelCursor) ?? GetLivingTargetNear(user, map, offset, shift, levelCursor)) is { } target)
        {
            aim = new MapCoordinates(_transform.GetWorldPosition(target), cursor.MapId);
            snapped = true;
        }

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

    // nearest living thing around the cursor on that level. going down it also has to be
    // showing through an opening, so nobody gets snapped onto under a roof
    private EntityUid? GetLivingTargetNear(
        EntityUid user,
        EntityUid map,
        int offset,
        Vector2 shift,
        MapCoordinates levelCursor)
    {
        EntityUid? best = null;
        var bestDistance = TargetSnapRadius * TargetSnapRadius;
        var box = Box2.CenteredAround(levelCursor.Position, new Vector2(TargetSnapRadius * 2));

        foreach (var entry in _spriteTree.QueryAabb(levelCursor.MapId, box))
        {
            var uid = entry.Uid;
            if (uid == user ||
                !_mobState.IsAlive(uid) ||
                !IsShootable(uid))
            {
                continue;
            }

            var position = _transform.GetWorldPosition(entry.Transform);
            var distance = (position - levelCursor.Position).LengthSquared();
            if (distance > bestDistance)
                continue;

            if (offset < 0 && !_zLevels.IsZShotPathOpen(map, position - shift, position - shift))
                continue;

            best = uid;
            bestDistance = distance;
        }

        return best;
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
