using System.Linq;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Power;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.CMU14.Hijack;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared._RMC14.Tools;
using Robust.Shared.Containers;

namespace Content.Server.CMU14.Hijack;

public sealed partial class ShipHijackSystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SharedContainerSystem _containers = default!;

    private void InitializeReactors()
    {
        SubscribeLocalEvent<CMUReactorOverloadComponent, InteractUsingEvent>(OnReactorTool,
            before: [typeof(SharedRMCPowerSystem)]);
        SubscribeLocalEvent<CMUReactorOverloadComponent, InteractHandEvent>(OnReactorHand,
            before: [typeof(SharedRMCPowerSystem)]);
        SubscribeLocalEvent<RMCFusionReactorComponent, CMUReactorOverloadDoAfterEvent>(OnOverloadFinished);
        SubscribeLocalEvent<CMUReactorOverloadComponent, ExaminedEvent>(OnReactorExamine);
        SubscribeLocalEvent<CMUHijackPumpComponent, BeforeDamageChangedEvent>(OnPumpDamageModify);
        SubscribeLocalEvent<CMUHijackPumpComponent, DamageChangedEvent>(OnPumpDamage);
        SubscribeLocalEvent<CMUHijackPumpComponent, ExaminedEvent>(OnPumpExamine);
    }

    private bool ReactorOperable(Entity<RMCFusionReactorComponent> reactor)
        => reactor.Comp.State == RMCFusionReactorState.Working && Transform(reactor).Anchored &&
           _containers.TryGetContainer(reactor, reactor.Comp.CellContainerSlot, out var cell) &&
           cell.ContainedEntities.Count > 0 &&
           _areas.TryGetArea(reactor.Owner.ToCoordinates(), out var area, out _) &&
           _power.IsAreaPowered(area.Value.Owner, RMCPowerChannel.Equipment);

    private void OnReactorTool(Entity<CMUReactorOverloadComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<MultitoolComponent>(args.Used) ||
            !TryGetShip(ent, out var ship) || !ship.Comp.SelfDestructUnlocked ||
            !_skills.HasSkill(args.User, "RMCSkillEngineer", 2) || !TryComp(ent, out RMCFusionReactorComponent? reactor))
            return;
        args.Handled = true;
        if (ship.Comp.Stage is CMUShipHijackStage.Detonating or CMUShipHijackStage.Destroyed || !ReactorOperable((ent, reactor)))
        {
            _popup.PopupEntity(Loc.GetString("cmu-hijack-reactor-inoperable"), ent, args.User);
            return;
        }
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, 2,
            new CMUReactorOverloadDoAfterEvent(), ent, ent, args.Used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BreakOnHandChange = true,
            NeedHand = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
        });
    }

    private void OnOverloadFinished(Entity<RMCFusionReactorComponent> ent, ref CMUReactorOverloadDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } tool || !HasComp<MultitoolComponent>(tool) ||
            !TryGetShip(ent, out var ship) || !ship.Comp.SelfDestructUnlocked ||
            ship.Comp.Stage is CMUShipHijackStage.Detonating or CMUShipHijackStage.Destroyed ||
            !_skills.HasSkill(args.User, "RMCSkillEngineer", 2) || !ReactorOperable(ent))
            return;
        args.Handled = true;
        var overload = EnsureComp<CMUReactorOverloadComponent>(ent);
        SetOverload((ent, overload), !overload.Overloaded);
        _popup.PopupEntity(Loc.GetString(overload.Overloaded ? "cmu-hijack-reactor-overloaded" : "cmu-hijack-reactor-restored"),
            ent, args.User);
        RecountOverloads(ship);
    }

    private void OnReactorHand(Entity<CMUReactorOverloadComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || !TryComp(ent, out CMUReactorOverloadComponent? overload) || !overload.Overloaded)
            return;
        args.Handled = true;
        if (HasComp<XenoComponent>(args.User))
        {
            SetOverload((ent, overload), false);
            if (TryGetShip(ent, out var ship))
                RecountOverloads(ship);
            _popup.PopupEntity(Loc.GetString("cmu-hijack-reactor-stopped"), ent, args.User);
        }
        else
            _popup.PopupEntity(Loc.GetString("cmu-hijack-reactor-safeties"), ent, args.User);
    }

    private void SetOverload(Entity<CMUReactorOverloadComponent> reactor, bool overloaded)
    {
        reactor.Comp.Overloaded = overloaded;
        Dirty(reactor);
        if (TryComp(reactor, out RMCFusionReactorComponent? fusion))
            _power.RefreshFusionReactorAppearance((reactor, fusion));
    }

    private void RecountOverloads(Entity<CMUShipHijackComponent> ship)
    {
        var count = 0;
        var query = EntityQueryEnumerator<CMUReactorOverloadComponent, RMCFusionReactorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var overload, out var reactor, out var transform))
        {
            if (transform.MapUid is not { } map || !ship.Comp.ShipMaps.Contains(map) || !overload.Overloaded)
                continue;
            if (!ReactorOperable((uid, reactor)))
            {
                SetOverload((uid, overload), false);
                continue;
            }
            count++;
        }
        count = Math.Min(count, ship.Comp.MaximumOverloadedGenerators);
        if (count == ship.Comp.OverloadedGenerators)
            return;
        ship.Comp.SelfDestructRemaining = CMUHijackMath.RescaleSelfDestruct(ship.Comp.SelfDestructRemaining,
            ship.Comp.OverloadedGenerators, count, ship.Comp.MaximumOverloadedGenerators);
        ship.Comp.OverloadedGenerators = count;
        if (count > 0 && !ship.Comp.GeneratorEverOverloaded)
        {
            ship.Comp.GeneratorEverOverloaded = true;
            _xenoAnnouncements.AnnounceAll(default, Loc.GetString("cmu-hijack-xeno-overload"));
        }
        Dirty(ship);
    }

    private void ProcessOverloads(Entity<CMUShipHijackComponent> ship)
    {
        RecountOverloads(ship);
        if (ship.Comp.OverloadedGenerators == 0)
            return;
        ship.Comp.SelfDestructRemaining = Math.Max(0, ship.Comp.SelfDestructRemaining - CMUHijackMath.TickSeconds);
        var duration = CMUHijackMath.SelfDestructWarningDuration(ship.Comp.OverloadedGenerators, ship.Comp.MaximumOverloadedGenerators);
        if (!ship.Comp.Heated && ship.Comp.SelfDestructRemaining <= duration * 0.66)
        {
            ship.Comp.Heated = true;
            HeatEngineRoom(ship, 363.15f);
            Announce(ship, "cmu-hijack-reactor-hot");
        }
        if (!ship.Comp.HalfwayAnnounced && ship.Comp.SelfDestructRemaining <= duration * 0.5)
        {
            ship.Comp.HalfwayAnnounced = true;
            Announce(ship, "cmu-hijack-reactor-halfway");
        }
        if (!ship.Comp.Superheated && ship.Comp.SelfDestructRemaining <= duration * 0.33)
        {
            ship.Comp.Superheated = true;
            HeatEngineRoom(ship, 393.15f);
            Announce(ship, "cmu-hijack-reactor-superheated");
        }
        if (ship.Comp.SelfDestructRemaining <= 0)
            BeginDetonation(ship);
        Dirty(ship);
    }

    private void OnReactorExamine(Entity<CMUReactorOverloadComponent> ent, ref ExaminedEvent args)
    {
        if (!TryGetShip(ent, out var ship) || !ship.Comp.SelfDestructUnlocked)
            return;
        args.PushMarkup(Loc.GetString(HasComp<CMUReactorOverloadComponent>(ent) && Comp<CMUReactorOverloadComponent>(ent).Overloaded
            ? "cmu-hijack-reactor-overloaded" : "cmu-hijack-reactor-multitool"));
        args.PushMarkup(Loc.GetString("cmu-hijack-reactor-countdown",
            ("generators", ship.Comp.OverloadedGenerators), ("seconds", (int) Math.Ceiling(ship.Comp.SelfDestructRemaining))));
    }

    private void OnPumpDamageModify(Entity<CMUHijackPumpComponent> ent, ref BeforeDamageChangedEvent args)
    {
        // Source pumps cannot be shot by marines, blown up, repaired, or sabotaged before hijack.
        var acid = args.Damage.DamageDict.TryGetValue("Caustic", out var caustic) && caustic > 0;
        if (ent.Comp.Broken || !TryGetShip(ent, out var ship) || ship.Comp.Stage == CMUShipHijackStage.Idle ||
            args.Damage.DamageDict.Values.Any(value => value < 0) ||
            args.Impact.Delivery == DamageImpactDelivery.Explosion ||
            (args.Impact.Delivery == DamageImpactDelivery.Projectile && !acid) ||
            (!HasComp<XenoComponent>(args.Origin) && !acid))
            args.Cancelled = true;
    }

    private void OnPumpDamage(Entity<CMUHijackPumpComponent> ent, ref DamageChangedEvent args)
    {
        var health = 1 - (float) _damage.GetTotalDamage((ent, args.Damageable)) / ent.Comp.Health;
        _appearance.SetData(ent, CMUHijackPumpVisuals.Health, health switch
        {
            <= 0 => 0,
            <= .3f => 30,
            <= .6f => 60,
            <= .8f => 80,
            <= .9f => 90,
            _ => 0,
        });
        if (ent.Comp.Broken || health > 0)
            return;
        ent.Comp.Broken = true;
        Dirty(ent);
        _appearance.SetData(ent, CMUHijackPumpVisuals.Broken, true);
        _ambient.SetAmbience(ent, false);
    }

    private void OnPumpExamine(Entity<CMUHijackPumpComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Broken)
            args.PushMarkup(Loc.GetString("cmu-hijack-pump-destroyed"));
        else if (TryComp(ent, out DamageableComponent? damage))
            args.PushMarkup(Loc.GetString("cmu-hijack-pump-integrity",
                ("integrity", Math.Max(0, (int) Math.Ceiling(100 - (double) _damage.GetTotalDamage((ent, damage)) / ent.Comp.Health * 100)))));
    }
}
