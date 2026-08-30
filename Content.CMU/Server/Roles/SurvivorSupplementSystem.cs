using System.Linq;
using Content.Server.CMU14.Round;
using Content.Shared.CMU14.Roles;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Roles;

public sealed partial class SurvivorSupplementSystem : EntitySystem
{
    [Dependency] private AuRoundSystem _auRound = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public void ApplyToSurvivor(EntityUid entity)
    {
        var mode = _auRound.SelectedPreset?.ID;
        if (mode == null)
            return;

        SurvivorSupplementPrototype? best = null;
        foreach (var supplement in _prototype.EnumeratePrototypes<SurvivorSupplementPrototype>())
        {
            if (supplement.BlacklistedGamemodes.Contains(mode, StringComparer.OrdinalIgnoreCase)
                || supplement.WhitelistedGamemodes.Count > 0
                && !supplement.WhitelistedGamemodes.Contains(mode, StringComparer.OrdinalIgnoreCase))
                continue;

            if (best == null
                || supplement.Priority > best.Priority
                || supplement.Priority == best.Priority
                && string.CompareOrdinal(supplement.ID, best.ID) < 0)
                best = supplement;
        }

        if (best == null)
            return;

        foreach (var entry in best.Entries)
        {
            if (entry.Skill is { } skill
                && !_skills.HasSkill(entity, skill, entry.Level))
                continue;

            foreach (var item in entry.Items)
                Give(entity, item);
        }
    }

    private void Give(EntityUid entity, EntProtoId item)
    {
        var spawn = Spawn(item, _transform.GetMoverCoordinates(entity));

        var slots = _inventory.GetSlotEnumerator(entity, SlotFlags.BACK);
        while (slots.MoveNext(out var slot))
        {
            if (slot.ContainedEntity is not { } pack
                || !TryComp(pack, out StorageComponent? storage))
                continue;

            if (_storage.Insert(pack, spawn, out _, storageComp: storage, playSound: false))
                return;
        }

        // Backpack full or absent: the item stays where it spawned, at the survivor's feet.
    }
}
