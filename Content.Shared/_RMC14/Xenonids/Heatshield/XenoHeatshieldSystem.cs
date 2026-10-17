using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Map;
using Content.Shared.Atmos.Components;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Heatshield;

public sealed partial class XenoHeatshieldSystem : EntitySystem
{
    private const string FireSpewEffectPrototype = "RMCEffectXenoFireSpew";
    private static readonly SoundSpecifier FireSpewSound = new SoundCollectionSpecifier("RMCFlamerShoot",
        AudioParams.Default.WithVolume(-6f).WithVariation(0.05f));

    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private EntityLookupSystem _entityLookup = default!;
    [Dependency] private SharedRMCFlammableSystem _flammable = default!;
    [Dependency] private SharedXenoHiveSystem _hive = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RMCMapSystem _rmcMap = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private XenoSystem _xeno = default!;

    private const float BileSprayTileRadius = 0.65f;
    private const int BileSprayLength = 3; // CMU14: triple the original one-row reach.

    private readonly HashSet<Entity<FlammableComponent>> _nearbyFlammables = new();
    private readonly HashSet<EntityUid> _bileSprayTargets = new();
    private readonly List<EntityCoordinates> _bileSprayTiles = new(); // CMU14

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoHeatshieldComponent, DamageModifyAfterResistEvent>(OnDamageModifyAfterResist);
        SubscribeLocalEvent<XenoHeatshieldComponent, GetMeleeDamageEvent>(OnGetMeleeDamage);
        SubscribeLocalEvent<XenoHeatshieldComponent, XenoVomitBileActionEvent>(OnVomitBileAction);
        SubscribeLocalEvent<XenoHeatshieldComponent, XenoSelfImmolateActionEvent>(OnSelfImmolateAction);
        SubscribeLocalEvent<XenoHeatshieldComponent, XenoSelfImmolateDoAfterEvent>(OnSelfImmolateDoAfter);
        SubscribeLocalEvent<XenoHeatshieldComponent, XenoThermoregulationActionEvent>(OnThermoregulationAction);
        SubscribeLocalEvent<XenoThermoregulatingComponent, GetMeleeAttackRateEvent>(OnThermoregulatingGetMeleeAttackRate);
        SubscribeLocalEvent<XenoThermoregulatingComponent, RefreshMovementSpeedModifiersEvent>(OnThermoregulatingRefreshSpeed);
    }

    private void OnDamageModifyAfterResist(Entity<XenoHeatshieldComponent> xeno, ref DamageModifyAfterResistEvent args)
    {
        args.Damage = new DamageSpecifier(args.Damage);
        foreach (var type in args.Damage.DamageDict.Keys.ToArray())
        {
            if (type == "Heat")
                args.Damage.DamageDict[type] *= xeno.Comp.FireDamageMultiplier;
        }
    }

    private void OnGetMeleeDamage(Entity<XenoHeatshieldComponent> xeno, ref GetMeleeDamageEvent args)
    {
        if (!_flammable.IsOnFire((xeno.Owner, null)))
            return;

        args.Damage += xeno.Comp.BurningMeleeDamage;
    }

    private void OnVomitBileAction(Entity<XenoHeatshieldComponent> xeno, ref XenoVomitBileActionEvent args)
    {
        if (args.Handled)
            return;

        if (args.Entity is { } entity &&
            _hive.FromSameHive(xeno.Owner, entity) &&
            TryUseBileOnEntity(xeno, entity, ref args))
        {
            return;
        }

        // CMU14: a selected enemy takes priority over fire on the ground beneath it.
        if (args.Entity is { } hostile && HasComp<FlammableComponent>(hostile) &&
            CanUseBileOnEntity(xeno, hostile) && TryUseBileSpray(xeno, args.Target, ref args))
        {
            return;
        }

        if (TryExtinguishTileFire(xeno, args.Target, ref args))
            return;

        if (args.Entity is { } sameHive && _hive.FromSameHive(xeno.Owner, sameHive))
        {
            _popup.PopupEntity(Loc.GetString("cm-xeno-heatshield-vomit-bile-no-fire"), sameHive, xeno);
            return;
        }

        if (TryUseBileSpray(xeno, args.Target, ref args))
            return;

        _popup.PopupEntity(Loc.GetString("cm-xeno-heatshield-vomit-bile-no-target"), xeno, xeno);
    }

    private bool TryUseBileOnEntity(Entity<XenoHeatshieldComponent> xeno, EntityUid target, ref XenoVomitBileActionEvent args)
    {
        if (!CanUseBileOnEntity(xeno, target))
            return false;

        if (!_rmcActions.TryUseAction(args))
            return true;

        args.Handled = true;
        ApplyBileToEntity(xeno, target);
        return true;
    }

    // CMU14: include the selected enemy and extend the existing three-wide spray to three rows.
    private bool TryUseBileSpray(Entity<XenoHeatshieldComponent> xeno, EntityCoordinates target, ref XenoVomitBileActionEvent args)
    {
        _bileSprayTargets.Clear();
        _bileSprayTiles.Clear();

        if (args.Entity is { } selected && HasComp<FlammableComponent>(selected) &&
            CanUseBileOnEntity(xeno, selected))
        {
            _bileSprayTargets.Add(selected);
        }

        var xenoCoords = _transform.GetMoverCoordinates(xeno);
        if (TryGetBileSprayVectors(xenoCoords, target, out var forward, out var side))
        {
            for (var distance = 1; distance <= BileSprayLength; distance++)
            {
                var center = _rmcMap.SnapToGrid(xenoCoords.Offset(forward * distance));
                _bileSprayTiles.Add(center.Offset(-side));
                _bileSprayTiles.Add(center);
                _bileSprayTiles.Add(center.Offset(side));
            }

            foreach (var tile in _bileSprayTiles)
                AddBileSprayTargets(xeno, tile);
        }

        if (_bileSprayTargets.Count == 0)
            return false;

        if (!_rmcActions.TryUseAction(args))
            return true;

        args.Handled = true;
        if (_flammable.IsOnFire((xeno.Owner, null)))
        {
            _audio.PlayPredicted(FireSpewSound, xeno, xeno);
            foreach (var tile in _bileSprayTiles)
                SpawnAtPosition(FireSpewEffectPrototype, tile);
        }

        foreach (var bileTarget in _bileSprayTargets)
        {
            if (CanUseBileOnEntity(xeno, bileTarget))
                ApplyBileToEntity(xeno, bileTarget);
        }

        return true;
    }

    private void AddBileSprayTargets(Entity<XenoHeatshieldComponent> xeno, EntityCoordinates coordinates)
    {
        _nearbyFlammables.Clear();
        _entityLookup.GetEntitiesInRange(coordinates, BileSprayTileRadius, _nearbyFlammables);

        foreach (var flammable in _nearbyFlammables)
        {
            if (CanUseBileOnEntity(xeno, flammable.Owner))
                _bileSprayTargets.Add(flammable.Owner);
        }
    }

    private bool CanUseBileOnEntity(Entity<XenoHeatshieldComponent> xeno, EntityUid target)
    {
        if (target == xeno.Owner || TerminatingOrDeleted(target))
            return false;

        if (_hive.FromSameHive(xeno.Owner, target))
            return _flammable.IsOnFire((target, null));

        return _xeno.CanAbilityAttackTarget(xeno, target);
    }

    private void ApplyBileToEntity(Entity<XenoHeatshieldComponent> xeno, EntityUid target)
    {
        if (_hive.FromSameHive(xeno.Owner, target))
        {
            _flammable.Extinguish((target, null));
            _popup.PopupEntity(Loc.GetString("cm-xeno-heatshield-vomit-bile-extinguish"), target, xeno);
            return;
        }

        if (_flammable.IsOnFire((xeno.Owner, null)))
            _flammable.Ignite((target, null), 4, 8, 16);
        else
            _flammable.AdjustStacks((target, null), 4);

        _popup.PopupEntity(Loc.GetString("cm-xeno-heatshield-vomit-bile-hostile"), target, xeno);
    }

    private bool TryExtinguishTileFire(Entity<XenoHeatshieldComponent> xeno, EntityCoordinates target, ref XenoVomitBileActionEvent args)
    {
        if (!_rmcMap.HasAnchoredEntityEnumerator<TileFireComponent>(target, out var fire))
            return false;

        if (!_rmcActions.TryUseAction(args))
            return true;

        args.Handled = true;
        QueueDel(fire.Owner);
        _popup.PopupEntity(Loc.GetString("cm-xeno-heatshield-vomit-bile-tile"), fire.Owner, xeno);
        return true;
    }

    private bool TryGetBileSprayVectors(
        EntityCoordinates xenoCoords,
        EntityCoordinates target,
        out Vector2 forward,
        out Vector2 side)
    {
        forward = default;
        side = default;

        var from = _transform.ToMapCoordinates(xenoCoords);
        var to = _transform.ToMapCoordinates(target);
        if (from.MapId != to.MapId)
            return false;

        var delta = to.Position - from.Position;
        if (delta.LengthSquared() == 0)
            return false;

        forward = Math.Abs(delta.X) >= Math.Abs(delta.Y)
            ? new Vector2(Math.Sign(delta.X), 0)
            : new Vector2(0, Math.Sign(delta.Y));
        side = new Vector2(-forward.Y, forward.X);

        return forward != Vector2.Zero;
    }

    private void OnSelfImmolateAction(Entity<XenoHeatshieldComponent> xeno, ref XenoSelfImmolateActionEvent args)
    {
        if (args.Handled || !_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        _flammable.AdjustStacks((xeno.Owner, null), 8);

        if (_flammable.IsOnFire((xeno.Owner, null)))
        {
            _flammable.Ignite((xeno.Owner, null), 4, 12, 24, false);
            _popup.PopupEntity(Loc.GetString("cm-xeno-heatshield-self-immolate"), xeno, xeno);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, xeno.Owner, TimeSpan.FromSeconds(1.5), new XenoSelfImmolateDoAfterEvent(), xeno.Owner)
        {
            BreakOnMove = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
        };

        _doAfter.TryStartDoAfter(doAfter);
        _popup.PopupEntity(Loc.GetString("cm-xeno-heatshield-self-immolate"), xeno, xeno);
    }

    private void OnSelfImmolateDoAfter(Entity<XenoHeatshieldComponent> xeno, ref XenoSelfImmolateDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        _flammable.Ignite((xeno.Owner, null), 4, 12, 24, false);
    }

    private void OnThermoregulationAction(Entity<XenoHeatshieldComponent> xeno, ref XenoThermoregulationActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_flammable.IsOnFire((xeno.Owner, null)))
        {
            _popup.PopupEntity(Loc.GetString("cm-xeno-heatshield-thermoregulation-not-burning"), xeno, xeno);
            return;
        }

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        var buff = EnsureComp<XenoThermoregulatingComponent>(xeno);
        buff.ExpiresAt = _timing.CurTime + TimeSpan.FromSeconds(5);
        Dirty(xeno.Owner, buff);
        _speed.RefreshMovementSpeedModifiers((xeno.Owner, null));
    }

    private void OnThermoregulatingRefreshSpeed(Entity<XenoThermoregulatingComponent> xeno, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(xeno.Comp.SpeedMultiplier, xeno.Comp.SpeedMultiplier);
    }

    private void OnThermoregulatingGetMeleeAttackRate(Entity<XenoThermoregulatingComponent> xeno, ref GetMeleeAttackRateEvent args)
    {
        args.Rate *= xeno.Comp.AttackRateMultiplier;
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<XenoThermoregulatingComponent>();
        while (query.MoveNext(out var uid, out var thermo))
        {
            if (time < thermo.ExpiresAt)
                continue;

            RemCompDeferred<XenoThermoregulatingComponent>(uid);
            _flammable.Extinguish((uid, null));
            _speed.RefreshMovementSpeedModifiers(uid);
        }
    }
}
