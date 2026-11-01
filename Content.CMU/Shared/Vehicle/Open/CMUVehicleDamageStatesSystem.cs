using Content.Shared._RMC14.Vehicle;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// Keeps the damage an open vehicle has taken by type, and works out which level of each of its
/// damage stages a part shows (see <see cref="CMUVehicleDamageStatesComponent"/>).
/// </summary>
public sealed class CMUVehicleDamageStatesSystem : EntitySystem
{
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehicleDamageStatesComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnDamageChanged(Entity<CMUVehicleDamageStatesComponent> ent, ref DamageChangedEvent args)
    {
        if (_net.IsClient || !args.DamageIncreased || args.DamageDelta is not { } delta)
            return;

        foreach (var (type, amount) in delta.DamageDict)
        {
            if (amount <= FixedPoint2.Zero)
                continue;

            ent.Comp.Taken[type] = ent.Comp.Taken.GetValueOrDefault(type) + amount;
        }

        Dirty(ent);
    }

    /// <summary>
    /// The level of a stage its part shows, in percent, or null below its first.
    /// </summary>
    public int? GetLevel(EntityUid vehicle, CMUVehicleDamageStatesComponent comp, CMUVehicleDamageStage stage)
    {
        var lost = PartWear(vehicle, stage);
        if (lost <= 0f || stage.Levels.Count == 0)
            return null;

        var percent = 100f * lost * (PartShare(vehicle, stage) ?? Share(comp, stage.DamageType));
        int? shown = null;
        foreach (var level in stage.Levels)
        {
            if (percent + 0.001f >= level)
                shown = level;
        }

        return shown;
    }

    /// <summary>
    /// How much of its integrity a stage's part has lost, 0 to 1.
    /// </summary>
    private float PartWear(EntityUid vehicle, CMUVehicleDamageStage stage)
    {
        switch (stage.Source)
        {
            case CMUVehicleDamageSource.Engine:
                return TryComp(vehicle, out CMUOpenVehicleComponent? jeep) && jeep.EngineMaxIntegrity > 0f
                    ? Math.Clamp(1f - jeep.EngineIntegrity / jeep.EngineMaxIntegrity, 0f, 1f)
                    : 0f;
            case CMUVehicleDamageSource.Slot:
                return stage.Slot != null &&
                       TryComp(vehicle, out ItemSlotsComponent? slots) &&
                       _itemSlots.TryGetSlot((vehicle, slots), stage.Slot, out var slot) &&
                       TryComp(slot.Item, out CMUVehiclePartIntegrityComponent? part) &&
                       part.MaxIntegrity > 0f
                    ? Math.Clamp(1f - part.Integrity / part.MaxIntegrity, 0f, 1f)
                    : 0f;
            default:
                return TryComp(vehicle, out HardpointIntegrityComponent? hull) && hull.MaxIntegrity > 0f
                    ? Math.Clamp(1f - hull.Integrity / hull.MaxIntegrity, 0f, 1f)
                    : 0f;
        }
    }

    /// <summary>
    /// A stage's damage type's (or group's) share of what wore its part, when the part keeps that
    /// (a panel, by the hits and crashes on its own side); null when it doesn't.
    /// </summary>
    private float? PartShare(EntityUid vehicle, CMUVehicleDamageStage stage)
    {
        if (stage.Source != CMUVehicleDamageSource.Slot ||
            stage.Slot == null ||
            !TryComp(vehicle, out ItemSlotsComponent? slots) ||
            !_itemSlots.TryGetSlot((vehicle, slots), stage.Slot, out var slot) ||
            !TryComp(slot.Item, out CMUVehiclePartIntegrityComponent? part) ||
            part.Wear.Count == 0)
        {
            return null;
        }

        var total = 0f;
        foreach (var amount in part.Wear.Values)
        {
            total += amount;
        }

        if (total <= 0f)
            return null;

        var mine = 0f;
        if (_prototypes.TryIndex<DamageGroupPrototype>(stage.DamageType, out var group))
        {
            foreach (var member in group.DamageTypes)
            {
                mine += part.Wear.GetValueOrDefault(member);
            }
        }
        else
        {
            mine = part.Wear.GetValueOrDefault(stage.DamageType);
        }

        return mine / total;
    }

    /// <summary>
    /// A damage type's (or group's) share of the damage the vehicle has taken, 0 to 1.
    /// </summary>
    private float Share(CMUVehicleDamageStatesComponent comp, string type)
    {
        var total = FixedPoint2.Zero;
        foreach (var amount in comp.Taken.Values)
        {
            total += amount;
        }

        if (total <= FixedPoint2.Zero)
            return 0f;

        var mine = FixedPoint2.Zero;
        if (_prototypes.TryIndex<DamageGroupPrototype>(type, out var group))
        {
            foreach (var member in group.DamageTypes)
            {
                mine += comp.Taken.GetValueOrDefault(member);
            }
        }
        else
        {
            mine = comp.Taken.GetValueOrDefault(type);
        }

        return (mine / total).Float();
    }
}
