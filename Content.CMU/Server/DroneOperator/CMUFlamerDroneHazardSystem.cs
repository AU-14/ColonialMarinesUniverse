using Content.Server.Emp;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Weapons.Ranged.Flamer;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CMU14.DroneOperator;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Examine;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.CMU14.DroneOperator;

/// <summary>Alternate fuels remain usable, but stress the drone and rupture when its chassis is wrecked.</summary>
public sealed partial class CMUFlamerDroneHazardSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private SharedRMCFlammableSystem _fire = default!;
    [Dependency] private ExplosionSystem _explosions = default!;
    [Dependency] private EmpSystem _emp = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUFlamerDroneFuelComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<CMUFlamerDroneFuelComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<CMUFlamerDroneFuelComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<CMUFlamerDroneFuelComponent, CMUFlamerFuelChangedEvent>(OnFuelChanged);
        SubscribeLocalEvent<CMUFlamerDroneFuelComponent, TakeAmmoEvent>(OnTakeAmmo);
        SubscribeLocalEvent<CMUFlamerDroneFuelComponent, GunShotEvent>(OnShot);
        SubscribeLocalEvent<CMUFlamerDroneFuelComponent, CMUCombatDroneWreckedEvent>(OnWrecked);
    }

    private void OnInit(Entity<CMUFlamerDroneFuelComponent> ent, ref MapInitEvent args) => RefreshFuel(ent);
    private void OnInserted(Entity<CMUFlamerDroneFuelComponent> ent, ref EntInsertedIntoContainerMessage args) => RefreshFuel(ent);
    private void OnRemoved(Entity<CMUFlamerDroneFuelComponent> ent, ref EntRemovedFromContainerMessage args) => RefreshFuel(ent);

    private void OnFuelChanged(Entity<CMUFlamerDroneFuelComponent> ent, ref CMUFlamerFuelChangedEvent args) => RefreshFuel(ent);

    private void OnTakeAmmo(Entity<CMUFlamerDroneFuelComponent> ent, ref TakeAmmoEvent args)
    {
        // Shooting can empty the tank before GunShotEvent. Keep the fired mixture until that event.
        ent.Comp.FiredFuel = (ent.Comp.Sticky, ent.Comp.Hot, ent.Comp.Volatile);
    }

    private bool TryGetFuel(EntityUid drone, out EntityUid tank, out Entity<SolutionComponent> solution)
    {
        tank = default;
        solution = default;
        if (!_containers.TryGetContainer(drone, SharedGunSystem.MagazineSlot, out var slot) ||
            slot.ContainedEntities.Count == 0)
            return false;
        tank = slot.ContainedEntities[0];
        if (!TryComp<RMCFlamerTankComponent>(tank, out var fuel) ||
            !_solutions.TryGetSolution(tank, fuel.SolutionId, out var found, out _))
            return false;
        solution = found.Value;
        return true;
    }

    private void RefreshFuel(Entity<CMUFlamerDroneFuelComponent> ent)
    {
        var fuel = ent.Comp;
        fuel.Sticky = fuel.Hot = fuel.Electrical = fuel.Volatile = fuel.Corrosive = 0;
        fuel.Glow = Color.FromHex("#ff8e32");
        if (!fuel.Ruined && TryGetFuel(ent, out _, out var solution) && solution.Comp.Solution.Volume > 0)
        {
            var contents = solution.Comp.Solution;
            foreach (var reagent in contents.Contents)
            {
                var fraction = (reagent.Quantity / contents.Volume).Float();
                switch (reagent.Reagent.Prototype)
                {
                    case "RMCNapalmB": case "RMCNapalmSticky": case "RMCBGel":
                        fuel.Sticky += fraction;
                        break;
                    case "RMCNapalmX": case "RMCNapalmHighCombustion":
                        fuel.Hot += fraction;
                        break;
                    case "RMCNapalmE": case "RMCNapalmEX":
                        fuel.Electrical += fraction;
                        break;
                    case "RMCR189":
                        fuel.Volatile += fraction;
                        break;
                    case "RMCCLF3":
                        fuel.Corrosive += fraction;
                        break;
                }
            }
            // Mixed fuels retain every drawback; the most concentrated family supplies the warning colour.
            var dominant = 0f;
            Tint(fuel.Sticky, "#65ff64");
            Tint(fuel.Hot, "#b5eaff");
            Tint(fuel.Electrical, "#cf7aff");
            Tint(fuel.Volatile, "#ff3535");
            Tint(fuel.Corrosive, "#e9ffc5");
            fuel.RepairEfficiency = MathF.Min(fuel.RepairEfficiency, 1f - 0.75f * fuel.Corrosive);

            void Tint(float fraction, string color)
            {
                if (fraction <= dominant)
                    return;
                dominant = fraction;
                fuel.Glow = Color.FromHex(color);
            }
        }
        Dirty(ent);
        _movement.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void OnShot(Entity<CMUFlamerDroneFuelComponent> ent, ref GunShotEvent args)
    {
        if (ent.Comp.Ruined)
            return;
        var fired = ent.Comp.FiredFuel ?? (ent.Comp.Sticky, ent.Comp.Hot, ent.Comp.Volatile);
        ent.Comp.FiredFuel = null;
        var hot = fired.Hot;
        var extraFuel = fired.Volatile * 3f;
        ent.Comp.CoolingUntil = _timing.CurTime + TimeSpan.FromSeconds(1.5f * fired.Sticky + 3f * hot);
        Dirty(ent);
        if (extraFuel > 0 && TryGetFuel(ent, out _, out var solution))
            _solutions.SplitSolution(solution, FixedPoint2.New(extraFuel));
        if (hot > 0)
            Damage(ent, "Heat", 6f * hot);
    }

    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;
        var drones = EntityQueryEnumerator<CMUFlamerDroneFuelComponent>();
        while (drones.MoveNext(out var uid, out var fuel))
        {
            if (Paused(uid) || fuel.Ruined)
                continue;
            if (fuel.ControlLockedUntil != TimeSpan.Zero && fuel.ControlLockedUntil <= time)
            {
                fuel.ControlLockedUntil = TimeSpan.Zero;
                Dirty(uid, fuel);
                _movement.RefreshMovementSpeedModifiers(uid);
            }
            if (fuel.Electrical > 0 && time >= fuel.NextFault)
            {
                fuel.NextFault = time + TimeSpan.FromSeconds(6);
                fuel.ControlLockedUntil = time + TimeSpan.FromSeconds(1.5f * fuel.Electrical);
                Dirty(uid, fuel);
                _movement.RefreshMovementSpeedModifiers(uid);
                Spawn("EffectSparks", Transform(uid).Coordinates);
            }
            if (time < fuel.NextWear || fuel.SpecialFraction <= 0)
                continue;
            fuel.NextWear = time + TimeSpan.FromSeconds(1);
            var volatileFuel = fuel.Volatile;
            var corrosion = fuel.Corrosive;
            if (corrosion > 0 && TryGetFuel(uid, out _, out var solution))
                _solutions.SplitSolution(solution, FixedPoint2.New(corrosion));
            Damage(uid, "Heat", volatileFuel);
            if (!fuel.Ruined)
                Damage(uid, "Caustic", 2f * corrosion);
        }

        var cookoffs = EntityQueryEnumerator<CMUFlamerFuelCookoffComponent>();
        while (cookoffs.MoveNext(out var uid, out var cookoff))
        {
            if (Paused(uid) || cookoff.Triggered || time < cookoff.At)
                continue;
            cookoff.Triggered = true;
            Rupture(Transform(uid).Coordinates, cookoff.Sticky, 0, 0, cookoff.Volatile, 0, uid);
            QueueDel(uid);
        }
    }

    private void Damage(EntityUid uid, string type, float amount)
    {
        if (amount <= 0)
            return;
        _damage.TryChangeDamage(uid, new DamageSpecifier { DamageDict = { [type] = FixedPoint2.New(amount) } },
            ignoreResistances: true, interruptsDoAfters: false);
    }

    private void OnWrecked(Entity<CMUFlamerDroneFuelComponent> ent, ref CMUCombatDroneWreckedEvent args)
    {
        if (ent.Comp.Ruined || !TryGetFuel(ent, out var tank, out var solution))
            return;
        RefreshFuel(ent);
        if (ent.Comp.SpecialFraction <= 0 || solution.Comp.Solution.Volume <= 0)
            return;
        var scale = Math.Clamp(solution.Comp.Solution.Volume.Float() / 200f, 0f, 1f);
        var sticky = ent.Comp.Sticky * scale;
        var hot = ent.Comp.Hot * scale;
        var electrical = ent.Comp.Electrical * scale;
        var volatileFuel = ent.Comp.Volatile * scale;
        var corrosive = ent.Comp.Corrosive * scale;
        // Commit the irreversible state before damage, container and chemistry callbacks can re-enter.
        ent.Comp.Ruined = true;
        Dirty(ent);
        _movement.RefreshMovementSpeedModifiers(ent.Owner);
        _solutions.RemoveAllSolution(solution);
        QueueDel(tank);
        var position = Transform(ent).Coordinates;
        if (sticky + volatileFuel > 0)
        {
            // The spill owns the delayed blast, so deleting or moving the wreck cannot cancel it.
            var spill = Spawn(null, position);
            var cookoff = AddComp<CMUFlamerFuelCookoffComponent>(spill);
            cookoff.At = _timing.CurTime + TimeSpan.FromSeconds(4);
            cookoff.Sticky = sticky;
            cookoff.Volatile = volatileFuel;
            _popup.PopupEntity(Loc.GetString("cmu-flamer-fuel-secondary"), ent);
        }
        Rupture(position, sticky, hot, electrical, volatileFuel, corrosive, ent);
    }

    private void Rupture(EntityCoordinates position, float sticky, float hot, float electrical, float volatileFuel, float corrosive, EntityUid cause)
    {
        var strength = sticky + hot + electrical + volatileFuel + corrosive;
        var radius = Math.Clamp((int) MathF.Ceiling(3f * MathF.Sqrt(strength)), 1, 3);
        var prototype = sticky > 0 ? "RMCTileFireGreen" : electrical > 0 ? "RMCTileFireNapalmEX" : "RMCTileFireBlue";
        _fire.SpawnFireDiamond(prototype, position, radius,
            intensity: Math.Max(1, (int) (30 * strength)), duration: Math.Max(1, (int) ((sticky > 0 ? 40 : 20) * strength)));
        if (hot + volatileFuel > 0)
            _explosions.QueueExplosion(cause, "Default", 60 * hot + 90 * volatileFuel, 5, 15, tileBreakScale: 0.2f);
        if (electrical > 0)
            _emp.EmpPulse(position, 4f * MathF.Sqrt(electrical), 1000 * electrical, TimeSpan.FromSeconds(6 * electrical));
        if (corrosive <= 0)
            return;
        var targets = new HashSet<Entity<DamageableComponent>>();
        _lookup.GetEntitiesInRange(position, radius, targets);
        foreach (var target in targets)
        {
            if (_examine.InRangeUnOccluded(cause, target, radius, entity => entity == cause || entity == target.Owner))
                Damage(target, "Caustic", 50 * corrosive);
        }
    }
}

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CMUFlamerFuelCookoffComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField] public TimeSpan At;
    [DataField] public float Sticky;
    [DataField] public float Volatile;
    [DataField] public bool Triggered;
}
