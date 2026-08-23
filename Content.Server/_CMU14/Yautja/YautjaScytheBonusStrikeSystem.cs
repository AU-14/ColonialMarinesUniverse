using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Weapons.Melee;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._CMU14.Yautja;

public sealed class YautjaScytheBonusStrikeSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaScytheBonusStrikeComponent, MeleeHitEvent>(
            OnScytheMeleeHit,
            after: [typeof(SharedRMCMeleeWeaponSystem)]);
    }

    private void OnScytheMeleeHit(Entity<YautjaScytheBonusStrikeComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit ||
            args.HitEntities.Count == 0 ||
            ent.Comp.Chance <= 0f ||
            !_random.Prob(ent.Comp.Chance))
        {
            return;
        }

        args.BonusDamage += args.BaseDamage;
        _popup.PopupEntity(
            Loc.GetString("cmu-yautja-scythe-bonus-strike-others"),
            args.User,
            Filter.PvsExcept(args.User),
            true,
            PopupType.MediumCaution);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-scythe-bonus-strike-self"), args.User, args.User, PopupType.MediumCaution);
    }
}
