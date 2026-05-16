using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.CameraShake;
using Content.Shared._RMC14.Emote;
using Content.Shared._RMC14.Entrenching;
using Content.Shared._RMC14.Gibbing;
using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Stun;

using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Explosion;
using Content.Shared.Hands;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Jittering;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using System.Numerics;
using Content.Shared._CMU14.Ape;
using Content.Shared._RMC14.Xenonids.Devour;

namespace Content.Shared._CMU14.Ape;
public abstract partial class SharedApeDestroySystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private AreaSystem _area = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private SharedDoAfterSystem _doafter = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private SharedRMCEmoteSystem _emote = default!;
    [Dependency] private RotateToFaceSystem _rotateToFace = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private MobStateSystem _mob = default!;
    [Dependency] protected IGameTiming _timing = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private EntityLookupSystem _entityLookup = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private RMCSizeStunSystem _size = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private RMCGibSystem _rmcGib = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private RMCCameraShakeSystem _cameraShake = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private RMCMapSystem _rmcMap = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RMCPullingSystem _rmcPull = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;

    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<ApeDestroyComponent, ApeDestroyActionEvent>(OnApeDestroyAction);
        SubscribeLocalEvent<ApeDestroyComponent, ApeDestroyLeapDoafter>(OnApeDestroyDoafter);

        SubscribeLocalEvent<ApeDestroyLeapingComponent, AttemptMobCollideEvent>(OnLeapCollide);
        SubscribeLocalEvent<ApeDestroyLeapingComponent, AttemptMobTargetCollideEvent>(OnLeapTargetCollide);

        SubscribeLocalEvent<ApeDestroyLeapingComponent, ComponentInit>(OnLeapingInit);
        SubscribeLocalEvent<ApeDestroyLeapingComponent, ComponentRemove>(OnLeapingRemove);
        SubscribeLocalEvent<ApeDestroyLeapingComponent, DropAttemptEvent>(OnLeapingCancel);
        SubscribeLocalEvent<ApeDestroyLeapingComponent, UseAttemptEvent>(OnLeapingCancel);
        SubscribeLocalEvent<ApeDestroyLeapingComponent, PickupAttemptEvent>(OnLeapingCancel);
        SubscribeLocalEvent<ApeDestroyLeapingComponent, AttackAttemptEvent>(OnLeapingCancel);
        SubscribeLocalEvent<ApeDestroyLeapingComponent, ThrowAttemptEvent>(OnLeapingCancel);
        SubscribeLocalEvent<ApeDestroyLeapingComponent, ChangeDirectionAttemptEvent>(OnLeapingCancel);
        SubscribeLocalEvent<ApeDestroyLeapingComponent, InteractionAttemptEvent>(OnLeapingCancelInteract);
        SubscribeLocalEvent<ApeDestroyLeapingComponent, PullAttemptEvent>(OnLeapingCancelPull);
        SubscribeLocalEvent<ApeDestroyLeapingComponent, UpdateCanMoveEvent>(OnLeapingCancel);
    }

    private void OnApeDestroyAction(Entity<ApeDestroyComponent> ape, ref ApeDestroyActionEvent args)
    {
        if (args.Handled || !_turf.TryGetTileRef(args.Target, out var tile))
            return;

        var target = _turf.GetTileCenter(tile.Value);

        if (!_interaction.InRangeUnobstructed(ape, target, ape.Comp.Range) || _rmcMap.IsTileBlocked(target))
        {
            _popup.PopupClient(Loc.GetString("rmc-destroy-cant-reach"), ape, ape, PopupType.SmallCaution);
            return;
        }

        if (!_area.TryGetArea(target, out var area, out var _) || area.Value.Comp.NoTunnel)
        {
            _popup.PopupClient(Loc.GetString("rmc-destroy-cant-area"), ape, ape, PopupType.SmallCaution);
            return;
        }

        _jitter.DoJitter(ape, ape.Comp.JumpTime, true, 80, 8, true);

        var doAfter = new DoAfterArgs(EntityManager, ape, ape.Comp.JumpTime, new ApeDestroyLeapDoafter(GetNetCoordinates(target)), ape)
        {
            BreakOnMove = true,
            BreakOnRest = true
        };

        _doafter.TryStartDoAfter(doAfter);
        Dirty(ape);
    }

    private void OnApeDestroyDoafter(Entity<ApeDestroyComponent> ape, ref ApeDestroyLeapDoafter args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (_net.IsClient)
            return;

        args.Handled = true;

        var coords = GetCoordinates(args.TargetCoords);

        if (!_interaction.InRangeUnobstructed(ape, coords, ape.Comp.Range) || _rmcMap.IsTileBlocked(coords))
        {
            _popup.PopupClient(Loc.GetString("rmc-destroy-cant-reach"), ape, ape, PopupType.SmallCaution);
            return;
        }

        _rotateToFace.TryFaceCoordinates(ape, _transform.ToMapCoordinates(args.TargetCoords).Position);
        _rmcPull.TryStopAllPullsFromAndOn(ape);

        if (_net.IsServer)
        {
            var leaping = EnsureComp<ApeDestroyLeapingComponent>(ape);
            leaping.Target = coords;
            leaping.LeapMoveAt = _timing.CurTime + ape.Comp.CrashTime / 2;
            leaping.LeapEndAt = _timing.CurTime + ape.Comp.CrashTime;
            Dirty(ape.Owner, leaping);

            var filter = Filter.Pvs(ape);
            Vector2 offset = _transform.ToMapCoordinates(coords).Position - _transform.GetMapCoordinates(ape).Position;

            var ev = new ApeDestroyLeapStartEvent(GetNetEntity(ape), offset);
            RaiseNetworkEvent(ev, filter);
        }

        PredictedSpawnAtPosition(ape.Comp.Telegraph, coords);

        _emote.TryEmoteWithChat(ape, ape.Comp.Emote);
    }

    private void OnLeapCollide(Entity<ApeDestroyLeapingComponent> ape, ref AttemptMobCollideEvent args)
    {
        args.Cancelled = true;
    }

    private void OnLeapTargetCollide(Entity<ApeDestroyLeapingComponent> ape, ref AttemptMobTargetCollideEvent args)
    {
        args.Cancelled = true;
    }

    private void OnLeapingCancel<T>(Entity<ApeDestroyLeapingComponent> ent, ref T args) where T : CancellableEntityEventArgs
    {
        args.Cancel();
    }

    private void OnLeapingCancelInteract(Entity<ApeDestroyLeapingComponent> ent, ref InteractionAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnLeapingCancelPull(Entity<ApeDestroyLeapingComponent> ent, ref PullAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void CrashDown(Entity<ApeDestroyComponent> ape)
    {
        RemCompDeferred<ApeDestroyLeapingComponent>(ape);

        if (_transform.GetGrid(ape.Owner) is not { } gridId || !TryComp<MapGridComponent>(gridId, out var grid))
            return;

        if (_net.IsServer)
            _audio.PlayPvs(ape.Comp.Sound, ape);

        foreach (var tile in _map.GetTilesIntersecting(gridId, grid, Box2.CenteredAround(_transform.GetMoverCoordinates(ape).Position, new Vector2(2, 2))))
        {
            //Gib mobs, knockback items, also kill structures
            foreach (var ent in _entityLookup.GetEntitiesInTile(tile, LookupFlags.All))
            {
                if (CanGib(ape, ent))
                {
                    if (!ape.Comp.Gibs || !TryComp<BodyComponent>(ent, out var body))
                    {
                        //just do a ton of damage instead
                        _damage.TryChangeDamage(ent, ape.Comp.MobDamage, true, origin: ape, tool: ape);
                        continue;
                    }

                    if (_net.IsServer)
                    {
                        _rmcGib.ScatterInventoryItems(ent);
                        _body.GibBody(ent, true, body);
                    }
                    continue;
                }

                if (HasComp<ItemComponent>(ent) && !Transform(ent).Anchored)
                {
                    _size.KnockBack(ent, _transform.GetMapCoordinates(ape), ape.Comp.Knockback, ape.Comp.Knockback, 15, true);
                    continue;
                }

                if (_whitelist.IsWhitelistPass(ape.Comp.Structures, ent))
                {
                    var ev = new GetExplosionResistanceEvent(ape.Comp.ExplosionType.Id);
                    RaiseLocalEvent(ent, ref ev);

                    _damage.TryChangeDamage(ent, ape.Comp.StructureDamage * ev.DamageCoefficient, true, origin: ape, tool: ape);
                    continue;
                }
            }

            PredictedSpawnAtPosition(ape.Comp.SmokeEffect, _turf.GetTileCenter(tile));
        }

        //Shake - effects everyone
        _mobs.Clear();
        _entityLookup.GetEntitiesInRange(Transform(ape).Coordinates, ape.Comp.ShakeCameraRange, _mobs);

        foreach (var mob in _mobs)
        {
            if (mob.Owner == ape.Owner)
            {
                _cameraShake.ShakeCamera(mob, 5, 1);
                continue;
            }

            _cameraShake.ShakeCamera(mob, 15, 1);
        }

        SetCooldown(ape);
    }

    private bool CanGib(EntityUid ape, EntityUid target)
    {
        if (ape == target)
            return false;

        if (HasComp<DevouredComponent>(target))
            return false;


        return HasComp<MobStateComponent>(target);
    }

    private void OnLeapingInit(Entity<ApeDestroyLeapingComponent> ape, ref ComponentInit args)
    {
        var actions = _actions.GetActions(ape);
        foreach (var action in actions)
        {
            _actions.SetEnabled(action.AsNullable(), false);
        }

        _blocker.UpdateCanMove(ape);
    }

    protected virtual void OnLeapingRemove(Entity<ApeDestroyLeapingComponent> ape, ref ComponentRemove args)
    {
        var actions = _actions.GetActions(ape);
        foreach (var action in actions)
        {
            _actions.SetEnabled(action.AsNullable(), true);
        }

        _blocker.UpdateCanMove(ape);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<ApeDestroyLeapingComponent, ApeDestroyComponent>();

        while (query.MoveNext(out var uid, out var leaping, out var destroy))
        {
            if (_mob.IsDead(uid))
            {
                RemCompDeferred<ApeDestroyLeapingComponent>(uid);
                continue;
            }

            if (leaping.LeapMoveAt != null && time > leaping.LeapMoveAt)
            {
                if (leaping.Target != null)
                    _transform.SetCoordinates(uid, leaping.Target.Value);

                leaping.LeapMoveAt = null;
                Dirty(uid, leaping);
            }

            if (leaping.LeapEndAt == null || time < leaping.LeapEndAt)
                continue;

            CrashDown((uid, destroy));
        }
    }

    private void SetCooldown(Entity<ApeDestroyComponent> ape)
    {
        // Find the ape's Destroy action and apply the configured cooldown to it.
        foreach (var (actionId, action) in _rmcActions.GetActionsWithEvent<ApeDestroyActionEvent>(ape))
        {
            _actions.SetCooldown(actionId, ape.Comp.Cooldown);
            break;
        }
    }


}


