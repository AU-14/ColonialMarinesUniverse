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
    // came to 95 bio, so fire did nothing, and mesh + clan armor came to 85 bullet so rifles mostly did nothing.
    // only the best worn piece counts, like it would on a single limb
    private void OnGetArmor(Entity<YautjaComponent> ent, ref CMGetArmorEvent args)
    {
        int meleeSum = 0, meleeBest = 0;
        int bulletSum = 0, bulletBest = 0;
        int bioSum = 0, bioBest = 0;
        var slots = _inventory.GetSlotEnumerator(ent.Owner, args.TargetSlots);
        while (slots.MoveNext(out var slot))
        {
            if (!TryComp(slot.ContainedEntity, out CMArmorComponent? piece) ||
                !_armor.AppliesTo(piece, args.TargetPart, args.TargetZone))
            {
                continue;
            }

            Track(piece.Melee, ref meleeSum, ref meleeBest);
            Track(piece.Bullet, ref bulletSum, ref bulletBest);
            Track(piece.Bio, ref bioSum, ref bioBest);
        }

        args.Melee -= meleeSum - meleeBest;
        args.Bullet -= bulletSum - bulletBest;
        args.Bio -= bioSum - bioBest;
    }

    private static void Track(int value, ref int sum, ref int best)
    {
        if (value <= 0)
            return;

        sum += value;
        best = Math.Max(best, value);
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
