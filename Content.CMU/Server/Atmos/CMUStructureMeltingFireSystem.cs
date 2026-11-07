using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Entrenching;
using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Xenonids.Acid;
using Content.Shared.CMU14.Atmos;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Atmos;

/// <summary>
/// FLOW-23: tile fires of Firepenetrating level 3+ melt the structures they burn on. Tile fires never damage
/// anchored structures on their own (static-on-static bodies produce no contacts), so this ticks every
/// <see cref="CMUStructureMeltingFireComponent.Interval"/> and deals
/// <c>Intensity * (Level - 2) * DamageMultiplier</c> Heat, resistances applied, to every damageable anchored
/// structure on the fire's tile. Structures without a <see cref="DamageableComponent"/> (indestructible hull
/// walls) are never touched. Server-only: the component is added server-side by the flamer fire spec.
/// </summary>
public sealed partial class CMUStructureMeltingFireSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private RMCMapSystem _rmcMap = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private IGameTiming _timing = default!;

    public const int MinimumMeltLevel = 3;

    private static readonly ProtoId<DamageTypePrototype> HeatType = "Heat";
    private static readonly ProtoId<TagPrototype> StructureTag = "Structure";
    private static readonly ProtoId<TagPrototype> WallTag = "Wall";

    private EntityQuery<BarricadeComponent> _barricadeQuery;
    private EntityQuery<CorrodibleComponent> _corrodibleQuery;
    private EntityQuery<DamageableComponent> _damageableQuery;
    private EntityQuery<DoorComponent> _doorQuery;
    private EntityQuery<TileFireComponent> _tileFireQuery;
    private EntityQuery<VehicleInteriorIndestructibleComponent> _vehicleIndestructibleQuery;

    private readonly List<EntityUid> _targets = new();

    public override void Initialize()
    {
        _barricadeQuery = GetEntityQuery<BarricadeComponent>();
        _corrodibleQuery = GetEntityQuery<CorrodibleComponent>();
        _damageableQuery = GetEntityQuery<DamageableComponent>();
        _doorQuery = GetEntityQuery<DoorComponent>();
        _tileFireQuery = GetEntityQuery<TileFireComponent>();
        _vehicleIndestructibleQuery = GetEntityQuery<VehicleInteriorIndestructibleComponent>();

        SubscribeLocalEvent<CMUStructureMeltingFireComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<CMUStructureMeltingFireComponent> ent, ref ComponentStartup args)
    {
        // The first melt lands one interval after the fire starts burning.
        ent.Comp.NextTick = _timing.CurTime + ent.Comp.Interval;
    }

    /// <summary>Heat dealt per tick before resistances; 0 below level 3.</summary>
    public static float MeltDamage(int level, int intensity, float multiplier = 1f)
    {
        if (level < MinimumMeltLevel || intensity <= 0)
            return 0f;

        return intensity * (level - 2) * multiplier;
    }

    /// <summary>
    /// Whether a melting fire may damage <paramref name="uid"/>: an anchored damageable structure (wall tag,
    /// structure tag, barricade, door, or an acid-corrodible structure) that is not another fire and not an
    /// indestructible vehicle interior piece.
    /// </summary>
    public bool IsMeltableStructure(EntityUid uid)
    {
        if (!_damageableQuery.HasComp(uid) ||
            _tileFireQuery.HasComp(uid) ||
            _vehicleIndestructibleQuery.HasComp(uid))
        {
            return false;
        }

        // Acid-immune things are immune to melting too.
        if (_corrodibleQuery.TryComp(uid, out var corrodible) && !corrodible.IsCorrodible)
            return false;

        if (_barricadeQuery.HasComp(uid) || _doorQuery.HasComp(uid))
            return true;

        // Reuse the acid notion of a structure (walls, girders, furniture, machines, crates).
        if (corrodible is { Structure: true })
            return true;

        return _tag.HasAnyTag(uid, WallTag, StructureTag);
    }

    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUStructureMeltingFireComponent>();
        while (query.MoveNext(out var uid, out var melt))
        {
            if (time < melt.NextTick)
                continue;

            melt.NextTick = time + melt.Interval;

            var amount = MeltDamage(melt.Level, melt.Intensity, melt.DamageMultiplier);
            if (amount <= 0)
                continue;

            if (TerminatingOrDeleted(uid))
                continue;

            // Snapshot first: melting can destroy and unanchor targets, mutating the tile's anchored set.
            _targets.Clear();
            var anchored = _rmcMap.GetAnchoredEntitiesEnumerator(uid);
            while (anchored.MoveNext(out var target))
            {
                if (target == uid)
                    continue;

                if (IsMeltableStructure(target))
                    _targets.Add(target);
            }

            if (_targets.Count == 0)
                continue;

            var damage = new DamageSpecifier(_prototypes.Index(HeatType), amount);
            foreach (var target in _targets)
            {
                if (TerminatingOrDeleted(target))
                    continue;

                var ev = new CMUFireMeltAttemptEvent(uid, melt.Level);
                RaiseLocalEvent(target, ref ev);
                if (ev.Cancelled)
                    continue;

                _damageable.TryChangeDamage(target, damage, interruptsDoAfters: false, origin: uid);
            }
        }
    }
}
