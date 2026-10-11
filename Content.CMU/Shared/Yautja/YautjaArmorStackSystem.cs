using Content.Shared._RMC14.Armor;
using Content.Shared.Explosion;
using Content.Shared.Inventory;

namespace Content.Shared.CMU14.Yautja;

public sealed partial class YautjaArmorStackSystem : EntitySystem
{
    [Dependency] private CMArmorSystem _armor = default!;
    [Dependency] private InventorySystem _inventory = default!;

    public override void Initialize()
    {
        // both run after the inventory relay has already added every worn piece
        SubscribeLocalEvent<YautjaComponent, CMGetArmorEvent>(OnGetArmor, after: [typeof(CMArmorSystem)]);
        SubscribeLocalEvent<YautjaComponent, GetExplosionResistanceEvent>(OnGetExplosionResistance, after: [typeof(InventorySystem)]);
    }

    // pred gear ports cmss13's per-limb armor onto every piece and rmc adds them all up. mesh + armor + greaves + mask
    // came to 95 bio, so fire did nothing. only the best worn piece counts, like it would on a single limb
    private void OnGetArmor(Entity<YautjaComponent> ent, ref CMGetArmorEvent args)
    {
        var sum = 0;
        var best = 0;
        var slots = _inventory.GetSlotEnumerator(ent.Owner, args.TargetSlots);
        while (slots.MoveNext(out var slot))
        {
            if (!TryComp(slot.ContainedEntity, out CMArmorComponent? piece) ||
                piece.Bio <= 0 ||
                !_armor.AppliesTo(piece, args.TargetPart, args.TargetZone))
            {
                continue;
            }

            sum += piece.Bio;
            best = Math.Max(best, piece.Bio);
        }

        args.Bio -= sum - best;
    }

    // same story for explosions, except each piece divides the damage, so a full kit compounded to ~25x and shrugged off grenades
    private void OnGetExplosionResistance(Entity<YautjaComponent> ent, ref GetExplosionResistanceEvent args)
    {
        var stacked = 1f;
        var best = 1f;
        var slots = _inventory.GetSlotEnumerator(ent.Owner, ~SlotFlags.POCKET);
        while (slots.MoveNext(out var slot))
        {
            if (!TryComp(slot.ContainedEntity, out CMArmorComponent? piece) || piece.ExplosionArmor <= 0)
                continue;

            var resist = CMArmorSystem.ExplosionResistFor(piece.ExplosionArmor);
            stacked *= resist;
            best = Math.Max(best, resist);
        }

        args.DamageCoefficient *= stacked / best;
    }
}
