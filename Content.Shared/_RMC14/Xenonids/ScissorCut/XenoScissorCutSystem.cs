using System.Numerics;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Emote;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Weapons.Melee;
using Content.Shared._RMC14.Xenonids.Empower;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;

namespace Content.Shared._RMC14.Xenonids.ScissorCut;

public sealed partial class XenoScissorCutSystem : EntitySystem
{
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private XenoSystem _xeno = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedColorFlashEffectSystem _colorFlash = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private RMCSlowSystem _slow = default!;
    [Dependency] private SharedRMCEmoteSystem _emote = default!;
    [Dependency] private SharedRMCMeleeWeaponSystem _rmcMelee = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private TurfSystem _turf = default!;

    private readonly HashSet<EntityUid> _areaHits = new();
    private readonly List<EntityUid> _destructibles = new();
    private readonly List<EntityUid> _mobs = new();
    private readonly List<EntityUid> _colorFlashTargets = new(1);

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoScissorCutComponent, XenoScissorCutActionEvent>(OnXenoScissorCutAction);
    }

    private void OnXenoScissorCutAction(Entity<XenoScissorCutComponent> xeno, ref XenoScissorCutActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        var slows = HasComp<XenoSuperEmpoweredComponent>(xeno);
        args.Handled = true;

        if (_transform.GetGrid(args.Target) is not { } gridId ||
    !TryComp(gridId, out MapGridComponent? grid))
            return;

        var direction = (args.Target.Position - _transform.GetMoverCoordinates(xeno).Position).Normalized().ToAngle() - Angle.FromDegrees(90);

        var xenoCoord = _transform.GetMoverCoordinates(xeno);
        var area = Box2.CenteredAround(xenoCoord.Position, new(1, xeno.Comp.Range)).Translated(new(0, (xeno.Comp.Range / 2) + 0.5f));
        var rot = new Box2Rotated(area, direction, xenoCoord.Position); // Correct the angle

        _destructibles.Clear();
        _mobs.Clear();

        if (_net.IsClient)
            return;

        _areaHits.Clear();
        _lookup.GetEntitiesIntersecting(gridId, rot, _areaHits, LookupFlags.Dynamic | LookupFlags.Static);
        foreach (var ent in _areaHits)
        {
            if (HasComp<DamageOnXenoScissorsComponent>(ent) || HasComp<DestroyOnXenoPierceScissorComponent>(ent))
            {
                _destructibles.Add(ent);
                continue;
            }

            if (!_xeno.CanAbilityAttackTarget(xeno, ent, false, true))
                continue;
            _mobs.Add(ent);
        }

        var selfCoords = _transform.GetMoverCoordinates(xeno);

        //Have to sort so multi fence destruction happens in order
        _destructibles.Sort((a, b) =>
        {
            var aDistance = selfCoords.TryDistance(EntityManager, a.ToCoordinates(), out var distanceA) ? distanceA : 10;
            var bDistance = selfCoords.TryDistance(EntityManager, b.ToCoordinates(), out var distanceB) ? distanceB : 10;
            return aDistance.CompareTo(bDistance);
        });

        foreach (var des in _destructibles)
        {
            if (!_interaction.InRangeUnobstructed(xeno.Owner, des, xeno.Comp.Range + 0.5f))
                continue;

            if (TryComp<DamageOnXenoScissorsComponent>(des, out var destruct))
            {
                var dam = _damage.TryChangeDamage(des, destruct.Damage, origin: xeno, tool: xeno);

                if (dam?.GetTotal() > FixedPoint2.Zero)
                {
                    var filter = Filter.Pvs(des, entityManager: EntityManager).RemoveWhereAttachedEntity(o => o == xeno.Owner);
                    RaiseColorFlash(des, filter);
                }

                continue;
            }

            if (!TryComp<DestroyOnXenoPierceScissorComponent>(des, out var destoy))
                continue;


            SpawnAtPosition(destoy.SpawnPrototype, des.ToCoordinates());
            QueueDel(des);

            _audio.PlayEntity(destoy.Sound, des, xeno);
            continue;
        }

        _emote.TryEmoteWithChat(xeno, xeno.Comp.Emote);

        //Now mobs
        EntityUid? hitEnt = null;
        foreach (var victim in _mobs)
        {
            if (!_interaction.InRangeUnobstructed(xeno.Owner, victim, xeno.Comp.Range + 0.5f))
                continue;

            if (hitEnt == null)
                hitEnt = victim;

            var change = _damage.TryChangeDamage(victim, xeno.Comp.Damage, origin: xeno, tool: xeno);

            if (change?.GetTotal() > FixedPoint2.Zero)
            {
                var filter = Filter.Pvs(victim, entityManager: EntityManager).RemoveWhereAttachedEntity(o => o == xeno.Owner);
                RaiseColorFlash(victim, filter);
            }

            SpawnAttachedTo(xeno.Comp.AttackEffect, victim.ToCoordinates());
            _audio.PlayEntity(xeno.Comp.SlashSound, xeno, victim);

            if (slows)
                _slow.TrySuperSlowdown(victim, xeno.Comp.SuperSlowDuration, ignoreDurationModifier: true);
        }

        if (hitEnt != null)
            _rmcMelee.DoLunge(xeno, hitEnt.Value);

        var bounds = rot.CalcBoundingBox();

        foreach (var tile in _map.GetTilesIntersecting(gridId, grid, rot))
        {
            if (!_interaction.InRangeUnobstructed(xeno.Owner, _turf.GetTileCenter(tile), xeno.Comp.Range + 0.5f))
                continue;

            var spawn = xeno.Comp.TelegraphEffect;

            if (!bounds.Encloses(Box2.CenteredAround(_turf.GetTileCenter(tile).Position, Vector2.One)))
                spawn = xeno.Comp.TelegraphEffectEdge;

            SpawnAtPosition(spawn, _turf.GetTileCenter(tile));
        }
    }

    private void RaiseColorFlash(EntityUid target, Filter filter)
    {
        _colorFlashTargets.Clear();
        _colorFlashTargets.Add(target);
        _colorFlash.RaiseEffect(Color.Red, _colorFlashTargets, filter);
    }
}
