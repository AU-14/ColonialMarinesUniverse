using System.Diagnostics.CodeAnalysis;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Random;

namespace Content.Shared.CMU14.Vehicle.Jeep;

public sealed partial class CMUJeepSystem
{
    // The lamps the headlight beams shine from; left is the driver's side.
    public const string DriverHeadlight = "headlight_left";
    public const string PassengerHeadlight = "headlight_right";

    private void InitializeLamps()
    {
        SubscribeLocalEvent<CMUJeepLampsComponent, ComponentInit>(OnLampsInit);
        SubscribeLocalEvent<CMUJeepLampsComponent, ComponentRemove>(OnLampsRemove);
    }

    /// <summary>
    /// Gives each lamp its slot on the jeep, on client and server alike. A jeep built without its
    /// lamps gets empty slots.
    /// </summary>
    private void OnLampsInit(Entity<CMUJeepLampsComponent> ent, ref ComponentInit args)
    {
        foreach (var lamp in ent.Comp.Lamps)
        {
            if (!ent.Comp.Fitted)
                lamp.Slot.StartingItem = null;

            _itemSlots.AddItemSlot(ent.Owner, lamp.SlotId, lamp.Slot);
        }
    }

    private void OnLampsRemove(Entity<CMUJeepLampsComponent> ent, ref ComponentRemove args)
    {
        foreach (var lamp in ent.Comp.Lamps)
        {
            _itemSlots.RemoveItemSlot(ent.Owner, lamp.Slot);
        }
    }

    public bool TryGetLamp(EntityUid vehicle, string part, [NotNullWhen(true)] out CMUJeepLampData? lamp)
    {
        lamp = null;
        if (!TryComp(vehicle, out CMUJeepLampsComponent? lamps))
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
        return TryComp(vehicle, out CMUJeepLampsComponent? lamps) &&
               lamps.Lamps.Find(l => l.Id == part) is { } lamp &&
               lamp.Slot.HasItem &&
               !lamps.Broken.Contains(part);
    }

    /// <summary>
    /// A hit on the jeep can catch any of its lamps.
    /// </summary>
    private void WearLamps(EntityUid vehicle, float damage)
    {
        if (!TryComp(vehicle, out CMUJeepLampsComponent? lamps))
            return;

        foreach (var lamp in lamps.Lamps)
        {
            if (_random.Prob(lamp.HitChance))
                WearPart(vehicle, lamp.SlotId, damage * lamp.DamageShare);
        }
    }

    private void RefreshLamps(Entity<ItemSlotsComponent?> vehicle)
    {
        if (!TryComp(vehicle, out CMUJeepLampsComponent? lamps))
            return;

        var broken = new List<string>();
        foreach (var lamp in lamps.Lamps)
        {
            if (IsBroken(vehicle, lamp.SlotId))
                broken.Add(lamp.Id);
        }

        if (broken.Count == lamps.Broken.Count && broken.TrueForAll(lamps.Broken.Contains))
            return;

        lamps.Broken = broken;
        Dirty(vehicle, lamps);
    }

    /// <summary>
    /// The clickable parts over the lamps, spawned with the jeep's other parts.
    /// </summary>
    private IEnumerable<CMUVehiclePartData> LampParts(EntityUid vehicle)
    {
        if (!TryComp(vehicle, out CMUJeepLampsComponent? lamps))
            yield break;

        foreach (var lamp in lamps.Lamps)
        {
            yield return new CMUVehiclePartData
            {
                Id = lamp.Id,
                Name = lamp.Name,
                Slot = lamp.SlotId,
                Offset = lamp.Offset,
            };
        }
    }
}
