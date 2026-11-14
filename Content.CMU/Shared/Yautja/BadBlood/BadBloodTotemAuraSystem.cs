using Content.Shared.CMU14.Nutrition;
using Content.Shared.CMU14.Yautja;

namespace Content.Shared.CMU14.Yautja.BadBlood;

// what standing in a Clan Totem's aura changes for a Bad Blood. shared, because hunger and thirst are predicted.
// the server decides who is in an aura and does the healing (BadBloodTotemSystem)
public sealed partial class BadBloodTotemAuraSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<BadBloodTotemAuraComponent, CMUSatiationRateEvent>(OnSatiationRate);
        SubscribeLocalEvent<BadBloodTotemAuraComponent, YautjaBracerRegenModifyEvent>(OnBracerRegenModify);
    }

    // only the drain is slowed. food and water still fill at their full rate
    private void OnSatiationRate(Entity<BadBloodTotemAuraComponent> ent, ref CMUSatiationRateEvent args)
    {
        if (args.Rate < 0f)
            args.Rate *= ent.Comp.DrainMultiplier;
    }

    private void OnBracerRegenModify(Entity<BadBloodTotemAuraComponent> ent, ref YautjaBracerRegenModifyEvent args)
    {
        args.Multiplier *= ent.Comp.BracerRegenMultiplier;
    }
}
