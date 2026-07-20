using System.Linq;
using System.Numerics;
using Content.Client._RMC14.ItemPickup;
using Content.Client._RMC14.Movement;
// RMC14
using Content.Client._RMC14.Vehicle;
using Content.Client._RMC14.Weapons.Ranged.Prediction;
// RMC14
using Content.Client.Animations;
using Content.Client.Clickable;
using Content.Client.Items;
using Content.Client.Weapons.Ranged.Components;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared._RMC14.Weapons.Ranged.Prediction;
using Content.Shared.CCVar;
using Content.Shared.CombatMode;
// RMC14
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
// RMC14
using Robust.Client.Animations;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Animations;
using Robust.Shared.Configuration;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using SharedGunSystem = Content.Shared.Weapons.Ranged.Systems.SharedGunSystem;
using TimedDespawnComponent = Robust.Shared.Spawners.TimedDespawnComponent;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem : SharedGunSystem
{
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private IInputManager _inputManager = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IStateManager _state = default!;
    [Dependency] private AnimationPlayerSystem _animPlayer = default!;
    [Dependency] private InputSystem _inputSystem = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    // RMC14
    [Dependency] private ItemPickupSystem _itemPickup = default!;
    [Dependency] private GunPredictionSystem _gunPrediction = default!;
    [Dependency] private RMCLagCompensationSystem _rmcLagCompensation = default!;
    [Dependency] private VehicleTurretMuzzleOffsetSystem _vehicleTurretMuzzleOffset = default!;
    // RMC14

    public static readonly EntProtoId HitscanProto = "HitscanEffect";
    private GunTargetEntityComparer _comparer = default!;

    public bool SpreadOverlay
    {
        get => _spreadOverlay;
        set
        {
            if (_spreadOverlay == value)
                return;

            _spreadOverlay = value;

            if (_spreadOverlay)
            {
                _overlayManager.AddOverlay(new GunSpreadOverlay(
                    EntityManager,
                    _eyeManager,
                    Timing,
                    _inputManager,
                    _player,
                    this,
                    TransformSystem));
            }
            else
            {
                _overlayManager.RemoveOverlay<GunSpreadOverlay>();
            }
        }
    }

    private bool _spreadOverlay;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesOutsidePrediction = true;
        SubscribeLocalEvent<AmmoCounterComponent, ItemStatusCollectMessage>(OnAmmoCounterCollect);
        SubscribeAllEvent<MuzzleFlashEvent>(OnMuzzleFlash);

        // Plays animated effects on the client.
        SubscribeNetworkEvent<HitscanEvent>(OnHitscan);

        InitializeMagazineVisuals();
        InitializeSpentAmmo();

        _comparer = new GunTargetEntityComparer();
    }

    private void OnUpdateClientAmmo(EntityUid uid, AmmoCounterComponent ammoComp, ref UpdateClientAmmoEvent args)
    {
        UpdateAmmoCount(uid, ammoComp, args.AritifialIncrease); //RMC14
    }

    private void OnMuzzleFlash(MuzzleFlashEvent args)
    {
        var gunUid = GetEntity(args.Uid);

        CreateEffect(gunUid, args, gunUid, _player.LocalEntity, args.Offset, args.OriginOffset); //RMC14
    }

    private void OnHitscan(HitscanEvent ev)
    {
        // ALL I WANT IS AN ANIMATED EFFECT

        // TODO EFFECTS
        // This is very jank
        // because the effect consists of three unrelatd entities, the hitscan beam can be split appart.
        // E.g., if a grid rotates while part of the beam is parented to the grid, and part of it is parented to the map.
        // Ideally, there should only be one entity, with one sprite that has multiple layers
        // Or at the very least, have the other entities parented to the same entity to make sure they stick together.
        foreach (var a in ev.Sprites)
        {
            if (a.Sprite is not SpriteSpecifier.Rsi rsi)
                continue;

            var coords = GetCoordinates(a.coordinates);

            if (!TryComp(coords.EntityId, out TransformComponent? relativeXform))
                continue;

            var ent = Spawn(HitscanProto, coords);
            var sprite = Comp<SpriteComponent>(ent);

            var xform = Transform(ent);
            var targetWorldRot = a.angle + _xform.GetWorldRotation(relativeXform);
            var delta = targetWorldRot - _xform.GetWorldRotation(xform);
            _xform.SetLocalRotationNoLerp(ent, xform.LocalRotation + delta, xform);

            sprite[EffectLayers.Unshaded].AutoAnimated = false;
            _sprite.LayerSetSprite((ent, sprite), EffectLayers.Unshaded, rsi);
            _sprite.LayerSetRsiState((ent, sprite), EffectLayers.Unshaded, rsi.RsiState);
            _sprite.SetScale((ent, sprite), new Vector2(a.Distance, 1f));
            sprite[EffectLayers.Unshaded].Visible = true;

            var anim = new Animation()
            {
                Length = TimeSpan.FromSeconds(0.48f),
                AnimationTracks =
                {
                    new AnimationTrackSpriteFlick()
                    {
                        LayerKey = EffectLayers.Unshaded,
                        KeyFrames =
                        {
                            new AnimationTrackSpriteFlick.KeyFrame(rsi.RsiState, 0f),
                        }
                    }
                }
            };

            _animPlayer.Play(ent, anim, "hitscan-effect");
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!Timing.IsFirstTimePredicted)
            return;

        var entityNull = _player.LocalEntity;

        if (entityNull == null || !TryComp<CombatModeComponent>(entityNull, out var combat) || !combat.IsInCombatMode)
        {
            return;
        }

        var entity = entityNull.Value;

        if (!TryGetGun(entity, out var gun))
        {
            return;
        }

        var useKey = gun.Comp.UseKey ? EngineKeyFunctions.Use : EngineKeyFunctions.UseSecondary;

        if (_inputSystem.CmdStates.GetState(useKey) != BoundKeyState.Down && !gun.Comp.BurstActivated)
        {
            if (gun.Comp.ShotCounter != 0)
                RaisePredictiveEvent(new RequestStopShootEvent { Gun = GetNetEntity(gun) });
            return;
        }

        if (gun.Comp.NextFire > Timing.CurTime)
            return;

        var mousePos = _eyeManager.PixelToMap(_inputManager.MouseScreenPosition);

        if (mousePos.MapId == MapId.Nullspace)
        {
            if (gun.Comp.ShotCounter != 0)
                RaisePredictiveEvent(new RequestStopShootEvent { Gun = GetNetEntity(gun) });

            return;
        }

        // Define target coordinates relative to gun entity, so that network latency on moving grids doesn't fuck up the target location.
        // RMC14
        var coordinateEntity = HasComp<GunUseGunOriginComponent>(gunUid) ? gunUid : entity;
        var coordinates = TransformSystem.ToCoordinates(coordinateEntity, mousePos);
        // RMC14

        NetEntity? target = null;
        if (_state.CurrentState is GameplayStateBase screen)
            target = GetNetEntity(screen.GetClickedEntity(mousePos));

        if (_player.LocalSession is not { } session)
            return;

        if (_itemPickup.RecentItemPickUp)
            return;

        Log.Debug($"Sending shoot request tick {Timing.CurTick} / {Timing.CurTime}");

        // RMC rearm instead of treating every held-fire request as continuous fire.
        var rearmSemiAuto =
            _cfg.GetCVar(CCVars.ControlHoldToAttackRanged) &&
            gun.SelectedMode == SelectiveFire.SemiAuto &&
            !HasComp<GunClickToFireComponent>(gunUid) &&
            (gun.AvailableModes & SelectiveFire.FullAuto) == 0;
        var projectiles = _gunPrediction.ShootRequested(GetNetEntity(gunUid), GetNetCoordinates(coordinates), target, null, session, rearmSemiAuto);

        RaisePredictiveEvent(new RequestShootEvent()
        {
            Target = target,
            Coordinates = GetNetCoordinates(coordinates),
            Gun = GetNetEntity(gunUid),
            RearmSemiAuto = rearmSemiAuto,
            Shot = projectiles?.Select(e => e.Id).ToList(),
            LastRealTick = _rmcLagCompensation.GetLastRealTick(null),
        });
    }

    protected override void Popup(string message, EntityUid? uid, EntityUid? user)
    {
        if (uid == null || user == null || !Timing.IsFirstTimePredicted)
            return;

        PopupSystem.PopupEntity(message, uid.Value, user.Value);
    }

    protected override void CreateEffect(EntityUid gunUid, MuzzleFlashEvent message, EntityUid? tracked = null, EntityUid? player = null, Vector2 offset = default, Vector2 originOffset = default)
    {
        if (!Timing.IsFirstTimePredicted)
            return;

        // EntityUid check added to stop throwing exceptions due to https://github.com/space-wizards/space-station-14/issues/28252
        // TODO: Check to see why invalid entities are firing effects.
        if (gunUid == EntityUid.Invalid)
        {
            Log.Debug($"Invalid Entity sent MuzzleFlashEvent (proto: {message.Prototype}, gun: {ToPrettyString(gunUid)})");
            return;
        }

        var gunXform = Transform(gunUid);
        var gridUid = gunXform.GridUid;
        EntityCoordinates coordinates;

        if (TryComp(gridUid, out MapGridComponent? mapGrid))
        {
            coordinates = new EntityCoordinates(gridUid.Value, _maps.LocalToGrid(gridUid.Value, mapGrid, gunXform.Coordinates));
        }
        else if (gunXform.MapUid != null)
        {
            coordinates = new EntityCoordinates(gunXform.MapUid.Value, TransformSystem.GetWorldPosition(gunXform));
        }
        else
        {
            return;
        }

        var ent = Spawn(message.Prototype, coordinates);
        TransformSystem.SetWorldRotationNoLerp(ent, message.Angle);

        // RMC14
        if (_vehicleTurretMuzzleOffset.TryGetGunPose(gunUid, null, out var origin, out var rotation))
        {
            var renderedMap = TransformSystem.ToMapCoordinates(origin);
            var effectXform = Transform(ent);
            effectXform.ActivelyLerping = false;
            var rotationOffset = (message.Angle - rotation).Reduced();
            TransformSystem.SetWorldRotationNoLerp((ent, effectXform), rotation + rotationOffset);
            TransformSystem.SetWorldPosition((ent, effectXform), renderedMap.Position + (rotation + rotationOffset).RotateVec(offset));

            var track = EnsureComp<VehicleTurretTrackedMuzzleFlashComponent>(ent);
            track.Weapon = gunUid;
            track.Offset = offset;
            track.RotationOffset = rotationOffset;
        }
        else if (tracked != null)
        {
            var track = EnsureComp<TrackUserComponent>(ent);
            track.User = tracked;
            track.Offset = offset; // RMC14
            track.OriginOffset = originOffset; // RMC14
        }
        // RMC14

        var lifetime = 0.4f;

        if (TryComp<TimedDespawnComponent>(gunUid, out var despawn))
        {
            lifetime = despawn.Lifetime;
        }

        var anim = new Animation()
        {
            Length = TimeSpan.FromSeconds(lifetime),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Color),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Color.White.WithAlpha(1f), 0),
                        new AnimationTrackProperty.KeyFrame(Color.White.WithAlpha(0f), lifetime)
                    }
                }
            }
        };

        _animPlayer.Play(ent, anim, "muzzle-flash");
        // RMC14
        if (!TryComp(ent, out PointLightComponent? light))
        {
            light = Factory.GetComponent<PointLightComponent>();
            light.NetSyncEnabled = false;
            AddComp(ent, light);
        }

        Lights.SetEnabled(ent, true, light);
        Lights.SetRadius(ent, 2f, light);
        Lights.SetColor(ent, Color.FromHex("#cc8e2b"), light);
        Lights.SetEnergy(ent, 5f, light);
        // RMC14

        var animTwo = new Animation()
        {
            Length = TimeSpan.FromSeconds(lifetime),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(PointLightComponent),
                    Property = nameof(PointLightComponent.Energy),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(5f, 0),
                        new AnimationTrackProperty.KeyFrame(0f, lifetime)
                    }
                },
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(PointLightComponent),
                    Property = nameof(PointLightComponent.AnimatedEnable),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(true, 0),
                        new AnimationTrackProperty.KeyFrame(false, lifetime)
                    }
                }
            }
        };

        // RMC14
        var uidPlayer = EnsureComp<AnimationPlayerComponent>(ent);

        _animPlayer.Stop(ent, uidPlayer, "muzzle-flash-light");
        _animPlayer.Play((ent, uidPlayer), animTwo, "muzzle-flash-light");
        // RMC14
    }

    public override void ShootProjectile(EntityUid uid,
        Vector2 direction,
        Vector2 gunVelocity,
        EntityUid? gunUid,
        EntityUid? user = null,
        float speed = 20)
    {
        EnsureComp<PredictedProjectileClientComponent>(uid);
        Physics.UpdateIsPredicted(uid);
        base.ShootProjectile(uid, direction, gunVelocity, gunUid, user, speed);
    }

    /// <remarks>We use our own sorting algorithm separate from the default for smarter configurability.</remarks>
    private NetEntity? GetBestTarget(IEye eye, MapCoordinates coordinates)
    {
        // Find all the entities intersecting our click
        var entities = _spriteTree.QueryAabb(coordinates.MapId, Box2.CenteredAround(coordinates.Position, new Vector2(1, 1)));

        // Check the entities against whether or not we can click them
        var foundEntities = new List<(EntityUid, bool, bool, int, uint, float, float)>(entities.Count);

        foreach (var entity in entities)
        {
            // Don't add the target if we can't shoot the target!
            if (!CheckFixtures(entity.Uid))
                continue;

            var entry = CheckTarget((entity.Uid, entity.Component, entity.Transform), eye, coordinates);
            foundEntities.Add(entry);
        }

        if (foundEntities.Count == 0)
            return null;

        // Do drawdepth & y-sorting. First index is the top-most sprite (opposite of normal render order).
        foundEntities.Sort(_comparer);
        var (target, alive, occluded, _, _, _, _) = foundEntities.FirstOrDefault();

        // Prevents us from just selecting a random target nearby our cursor. It must either be alive, or our cursor must be on top of it!
        if (!occluded && !alive)
            return null;

        return GetNetEntity(target);
    }

    private (EntityUid, bool, bool, int, uint, float, float) CheckTarget(Entity<SpriteComponent, TransformComponent> target, IEye eye, MapCoordinates coordinates)
    {
        var occluded = _clickable.CheckClick((target.Owner, null, target.Comp1, target.Comp2),
            coordinates.Position,
            eye,
            true,
            out var drawDepthClicked,
            out var renderOrder,
            out var bottom);

        var difference = (target.Comp2.Coordinates.Position - coordinates.Position).LengthSquared();

        return (target.Owner, _mobState.IsAlive(target.Owner), occluded, drawDepthClicked, renderOrder, bottom, difference);
    }

    /// <summary>
    /// This Comparer takes a list of Entities that we can hit and orders them by which target the player is probably trying to shoot.
    /// We organize based on these criteria in this order:
    /// alive means the entity has a MobState and is currently alive. We check it first since they typically shoot back.
    /// occluded is whether the cursor is above the sprite or just near it.
    /// depth is the order in which sprites are layered, bigger number means its rendered above others.
    /// renderOrder is used to indicate if a sprite should be visually more important, typically this value is 0.
    /// bottom indicates which sprite is visually the lowest on the screen and therefore typically above other sprites.
    /// distance indicates the distance from the entity's coordinates to our mouse.
    /// If all of those tie, then we organize by whichever entity has the highest EntityUid.
    /// </summary>
    private sealed class GunTargetEntityComparer : IComparer<(EntityUid clicked, bool alive, bool occluded, int depth, uint renderOrder, float bottom, float distance)>
    {
        public int Compare((EntityUid clicked, bool alive, bool occluded, int depth, uint renderOrder, float bottom, float distance) x,
            (EntityUid clicked, bool alive, bool occluded, int depth, uint renderOrder, float bottom, float distance) y)
        {
            var cmp = y.occluded.CompareTo(x.occluded);

            if (cmp != 0)
            {
                return cmp;
            }

            cmp = y.alive.CompareTo(x.alive);
            if (cmp != 0)
            {
                return cmp;
            }

            cmp = y.depth.CompareTo(x.depth);
            if (cmp != 0)
            {
                return cmp;
            }

            cmp = y.renderOrder.CompareTo(x.renderOrder);

            if (cmp != 0)
            {
                return cmp;
            }

            cmp = -y.bottom.CompareTo(x.bottom);

            if (cmp != 0)
            {
                return cmp;
            }

            cmp = -y.distance.CompareTo(x.distance);

            if (cmp != 0)
            {
                return cmp;
            }

            return y.clicked.CompareTo(x.clicked);
        }
    }

    private bool CheckFixtures(Entity<FixturesComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return false;

        // TODO: Maybe also check that our cursor is intersecting a valid fixture?
        foreach (var fix in entity.Comp.Fixtures)
        {
            if (!fix.Value.Hard || (fix.Value.CollisionLayer & (int)CollisionGroup.BulletImpassable) == 0)
                continue;

            // Only need to check if we're hitting one fixture
            return true;
        }

        // If we cannot collide then we absolutely do not want to target it!
        return false;
    }

    public override void PlayImpactSound(EntityUid otherEntity, DamageSpecifier? modifiedDamage, SoundSpecifier? weaponSound, bool forceWeaponSound) { }
}
