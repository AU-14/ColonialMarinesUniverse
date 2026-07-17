using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared._CMU14.Yautja;

public sealed partial class YautjaBracerAttachmentSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaBracerAttachmentSpeedBonusComponent, GetMeleeAttackRateEvent>(OnGetMeleeAttackRate);
    }

    private void OnGetMeleeAttackRate(Entity<YautjaBracerAttachmentSpeedBonusComponent> ent, ref GetMeleeAttackRateEvent args)
    {
        if (ent.Comp.PairedAttackSeconds <= 0f ||
            !_hands.IsHolding(args.User, args.Weapon) ||
            !HasAnotherBracerAttachment(args.User, args.Weapon))
        {
            return;
        }

        args.Rate = 1f / ent.Comp.PairedAttackSeconds;
    }

    private bool HasAnotherBracerAttachment(EntityUid user, EntityUid weapon)
    {
        if (!TryComp(user, out HandsComponent? hands))
            return false;

        foreach (var held in _hands.EnumerateHeld((user, hands)))
        {
            if (held == weapon)
                continue;

            if (TryComp(held, out YautjaStoredGearComponent? gear) &&
                IsBracerAttachment(gear.Kind))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsBracerAttachment(YautjaGearKind kind)
    {
        return kind is YautjaGearKind.WristBlades
            or YautjaGearKind.Scimitar
            or YautjaGearKind.Shield
            or YautjaGearKind.ChainGauntlet;
    }
}
