using Content.Shared.Weapons.Melee;

namespace Content.Shared.CMU14.Yautja.BadBlood;

// ECS-401: Yautja melee weapons forbid wide swings in YAML, which is what honourable hunters get. A Bad Blood
// swings them wide anyway, except the chain gauntlet: its right click is the shove into the throw finisher.
public sealed partial class BadBloodWideSwingSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaBadBloodComponent, CMUWideAttackOverrideEvent>(OnWideAttackOverride);
    }

    private void OnWideAttackOverride(Entity<YautjaBadBloodComponent> ent, ref CMUWideAttackOverrideEvent args)
    {
        if (!HasComp<YautjaTechItemComponent>(args.Weapon) || HasComp<YautjaChainGauntletComponent>(args.Weapon))
            return;

        args.Allowed = true;
    }
}
