using System.Numerics;
using System.Diagnostics.CodeAnalysis;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Random;

namespace Content.Shared.CMU14.Vehicle.Open;

public sealed partial class CMUOpenVehicleSystem
{
    // The lamps the headlight beams shine from; left is the driver's side.
    public const string DriverHeadlight = "headlight_left";
    public const string PassengerHeadlight = "headlight_right";

    private void InitializeLamps()
    {
        SubscribeLocalEvent<CMUVehicleLampsComponent, ComponentInit>(OnLampsInit);
        SubscribeLocalEvent<CMUVehicleLampsComponent, ComponentRemove>(OnLampsRemove);
    }

    /// <summary>
    /// Gives each lamp its slot on the jeep, on client and server alike. A jeep built without its
    /// lamps gets empty slots.
    /// </summary>
    private void OnLampsInit(Entity<CMUVehicleLampsComponent> ent, ref ComponentInit args)
    {
        foreach (var lamp in ent.Comp.Lamps)
        {
            if (!ent.Comp.Fitted)
                lamp.Slot.StartingItem = null;

            _itemSlots.AddItemSlot(ent.Owner, lamp.SlotId, lamp.Slot);
        }
    }

    private void OnLampsRemove(Entity<CMUVehicleLampsComponent> ent, ref ComponentRemove args)
    {
        foreach (var lamp in ent.Comp.Lamps)
        {
            _itemSlots.RemoveItemSlot(ent.Owner, lamp.Slot);
        }
    }

    public bool TryGetLamp(EntityUid vehicle, string part, [NotNullWhen(true)] out CMUVehicleLampData? lamp)
    {
        lamp = null;
        if (!TryComp(vehicle, out CMUVehicleLampsComponent? lamps))
            return false;

        lamp = lamps.Lamps.Find(l => l.Id == part);
        return lamp != null;
    }

    public bool IsLamp(EntityUid vehicle, string part)
    {
        return TryGetLamp(vehicle, part, out _);
    }

    /// <summary>
    /// Whether a lamp is fitted and whole enough to light.
    /// </summary>
    public bool LampWorks(EntityUid vehicle, string part)
    {
        return TryComp(vehicle, out CMUVehicleLampsComponent? lamps) &&
               lamps.Lamps.Find(l => l.Id == part) is { } lamp &&
               lamp.Slot.HasItem &&
               !lamps.Broken.Contains(part);
    }

    /// <summary>
    /// A hit on the jeep can catch any of its lamps.
    /// </summary>
    private void WearLamps(EntityUid vehicle, float damage)
    {
        if (!TryComp(vehicle, out CMUVehicleLampsComponent? lamps))
            return;

        foreach (var lamp in lamps.Lamps)
        {
            if (_random.Prob(lamp.HitChance))
                WearPart(vehicle, lamp.SlotId, damage * lamp.DamageShare);
        }
    }

    /// <summary>
    /// A crash breaks the lamps near where it hits: none below the lamps' slowest crash, surely at
    /// their sure speed right at the lamp, less likely the farther off it hits.
    /// </summary>
    private void BreakLampsInCrash(Entity<CMUOpenVehicleComponent> ent, CMUVehicleCrashedEvent crash)
    {
        if (crash.Contact is not { } contact ||
            !TryComp(ent, out CMUVehicleLampsComponent? lamps) ||
            lamps.CrashSureSpeed <= lamps.CrashMinSpeed ||
            lamps.CrashReach <= 0f)
        {
            return;
        }

        var force = Math.Clamp((crash.Speed - lamps.CrashMinSpeed) / (lamps.CrashSureSpeed - lamps.CrashMinSpeed), 0f, 1f);
        if (force <= 0f)
            return;

        var broke = false;
        foreach (var lamp in lamps.Lamps)
        {
            var near = 1f - Vector2.Distance(contact, lamp.Offset) / lamps.CrashReach;
            if (near <= 0f ||
                !_random.Prob(Math.Clamp(force * near, 0f, 1f)) ||
                !TryGetPartSlot(ent.Owner, lamp.SlotId, out var slot) ||
                !TryComp(slot.Item, out CMUVehiclePartIntegrityComponent? part) ||
                part.Integrity <= part.MaxIntegrity * part.BrokenFraction)
            {
                continue;
            }

            WearPart(ent, lamp.SlotId, part.Integrity);
            broke = true;
        }

        if (broke)
            RefreshParts(ent, false);
    }

    /// <summary>
    /// A shot landing on a lamp shatters it, however light: the nearest within reach.
    /// </summary>
    private void ShootLamp(Entity<CMUOpenVehicleComponent> ent, Vector2 hit)
    {
        if (!TryComp(ent, out CMUVehicleLampsComponent? lamps))
            return;

        CMUVehicleLampData? nearest = null;
        var best = lamps.ShotReach;
        foreach (var lamp in lamps.Lamps)
        {
            var distance = Vector2.Distance(hit, lamp.Offset);
            if (distance > best)
                continue;

            best = distance;
            nearest = lamp;
        }

        if (nearest != null &&
            TryGetPartSlot(ent.Owner, nearest.SlotId, out var slot) &&
            TryComp(slot.Item, out CMUVehiclePartIntegrityComponent? part) &&
            part.Integrity > part.MaxIntegrity * part.BrokenFraction)
        {
            WearPart(ent, nearest.SlotId, part.Integrity);
        }
    }

    /// <returns>Whether a lamp broke or was mended.</returns>
    private bool RefreshLamps(Entity<ItemSlotsComponent?> vehicle)
    {
        if (!TryComp(vehicle, out CMUVehicleLampsComponent? lamps))
            return false;

        var broken = new List<string>();
        foreach (var lamp in lamps.Lamps)
        {
            if (IsBroken(vehicle, lamp.SlotId))
                broken.Add(lamp.Id);
        }

        if (broken.Count == lamps.Broken.Count && broken.TrueForAll(lamps.Broken.Contains))
            return false;

        lamps.Broken = broken;
        Dirty(vehicle, lamps);
        return true;
    }

    /// <summary>
    /// The clickable parts over the lamps, spawned with the jeep's other parts.
    /// </summary>
    private IEnumerable<CMUVehiclePartData> LampParts(EntityUid vehicle)
    {
        if (!TryComp(vehicle, out CMUVehicleLampsComponent? lamps))
            yield break;

        foreach (var lamp in lamps.Lamps)
        {
            yield return new CMUVehiclePartData
            {
                Id = lamp.Id,
                Name = lamp.Name,
                Slot = lamp.SlotId,
                Offset = lamp.Offset,
                OverRiders = lamp.OverRiders,
            };
        }
    }
}
