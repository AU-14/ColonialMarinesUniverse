using Content.Shared.Examine;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// Applies <see cref="BadBloodForgedComponent"/>: a forged bracer weapon hits harder and says so on examine.
/// </summary>
public sealed class BadBloodForgeSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<BadBloodForgedComponent, GetMeleeDamageEvent>(OnGetMeleeDamage);
        SubscribeLocalEvent<BadBloodForgedComponent, ExaminedEvent>(OnExamined);
    }

    private void OnGetMeleeDamage(Entity<BadBloodForgedComponent> ent, ref GetMeleeDamageEvent args)
    {
        args.Damage *= ent.Comp.Multiplier;
    }

    private void OnExamined(Entity<BadBloodForgedComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("cmu-bad-blood-forged-examine"));
    }

    /// <summary>
    /// Forges <paramref name="weapon"/>. False if it already is.
    /// </summary>
    public bool TryForge(EntityUid weapon, float multiplier)
    {
        if (HasComp<BadBloodForgedComponent>(weapon))
            return false;

        var forged = EnsureComp<BadBloodForgedComponent>(weapon);
        forged.Multiplier = multiplier;
        Dirty(weapon, forged);
        return true;
    }
}
