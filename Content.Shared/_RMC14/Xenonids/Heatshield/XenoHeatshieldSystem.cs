using System.Linq;
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
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Heatshield;

public sealed partial class XenoHeatshieldSystem : EntitySystem
{
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
    [Dependency] private XenoSystem _xeno = default!;

    private readonly HashSet<Entity<FlammableComponent>> _nearbyFlammables = new();

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

        if (args.Entity is { } entity && TryUseBileOnEntity(xeno, entity, ref args))
            return;

        if (TryExtinguishTileFire(xeno, args.Target, ref args))
            return;

        if (args.Entity is { } sameHive && _hive.FromSameHive(xeno.Owner, sameHive))
        {
            _popup.PopupClient(Loc.GetString("cm-xeno-heatshield-vomit-bile-no-fire"), sameHive, xeno);
            return;
        }

        if (TryFindBileTarget(xeno, args.Target, out var target) &&
            TryUseBileOnEntity(xeno, target, ref args))
        {
            return;
        }

        _popup.PopupClient(Loc.GetString("cm-xeno-heatshield-vomit-bile-no-target"), xeno, xeno);
    }

    private bool TryUseBileOnEntity(Entity<XenoHeatshieldComponent> xeno, EntityUid target, ref XenoVomitBileActionEvent args)
    {
        if (TerminatingOrDeleted(target))
            return false;

        if (_hive.FromSameHive(xeno.Owner, target))
        {
            if (!_flammable.IsOnFire((target, null)))
                return false;

            if (!_rmcActions.TryUseAction(args))
                return true;

            args.Handled = true;
            _flammable.Extinguish((target, null));
            _popup.PopupClient(Loc.GetString("cm-xeno-heatshield-vomit-bile-extinguish"), target, xeno);
            return true;
        }

        if (!_xeno.CanAbilityAttackTarget(xeno, target))
            return false;

        if (!_rmcActions.TryUseAction(args))
            return true;

        args.Handled = true;
        if (_flammable.IsOnFire((xeno.Owner, null)))
            _flammable.Ignite((target, null), 4, 8, 16);
        else
            _flammable.AdjustStacks((target, null), 4);

        _popup.PopupClient(Loc.GetString("cm-xeno-heatshield-vomit-bile-hostile"), target, xeno);
        return true;
    }

    private bool TryExtinguishTileFire(Entity<XenoHeatshieldComponent> xeno, EntityCoordinates target, ref XenoVomitBileActionEvent args)
    {
        if (!_rmcMap.HasAnchoredEntityEnumerator<TileFireComponent>(target, out var fire))
            return false;

        if (!_rmcActions.TryUseAction(args))
            return true;

        args.Handled = true;
        QueueDel(fire.Owner);
        _popup.PopupClient(Loc.GetString("cm-xeno-heatshield-vomit-bile-tile"), fire.Owner, xeno);
        return true;
    }

    private bool TryFindBileTarget(Entity<XenoHeatshieldComponent> xeno, EntityCoordinates target, out EntityUid found)
    {
        found = default;
        _nearbyFlammables.Clear();
        _entityLookup.GetEntitiesInRange(target, 0.45f, _nearbyFlammables);

        foreach (var flammable in _nearbyFlammables)
        {
            if (flammable.Owner == xeno.Owner || TerminatingOrDeleted(flammable.Owner))
                continue;

            if (!_hive.FromSameHive(xeno.Owner, flammable.Owner) ||
                !_flammable.IsOnFire((flammable.Owner, null)))
            {
                continue;
            }

            found = flammable.Owner;
            return true;
        }

        foreach (var flammable in _nearbyFlammables)
        {
            if (flammable.Owner == xeno.Owner || TerminatingOrDeleted(flammable.Owner))
                continue;

            if (!_xeno.CanAbilityAttackTarget(xeno, flammable.Owner))
                continue;

            found = flammable.Owner;
            return true;
        }

        return false;
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
            _popup.PopupClient(Loc.GetString("cm-xeno-heatshield-self-immolate"), xeno, xeno);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, xeno.Owner, TimeSpan.FromSeconds(1.5), new XenoSelfImmolateDoAfterEvent(), xeno.Owner)
        {
            BreakOnMove = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
        };

        _doAfter.TryStartDoAfter(doAfter);
        _popup.PopupClient(Loc.GetString("cm-xeno-heatshield-self-immolate"), xeno, xeno);
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
            _popup.PopupClient(Loc.GetString("cm-xeno-heatshield-thermoregulation-not-burning"), xeno, xeno);
            return;
        }

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        var buff = EnsureComp<XenoThermoregulatingComponent>(xeno);
        buff.ExpiresAt = _timing.CurTime + TimeSpan.FromSeconds(5);
        Dirty(xeno.Owner, buff);
        _speed.RefreshMovementSpeedModifiers(xeno);
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
