using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body.Events;
using Content.Shared.CMU14.Traits;
using Content.Shared.StatusEffectNew;

namespace Content.Shared.CMU14.Medical.Core;

// the CMU wound and internal bleed drains check this in CMUBloodSystem.GetBleedMultiplier, fractures in
// SharedBoneSystem and limb loss in BodyPartSeveranceSystem. this covers the vanilla and RMC bleed paths
public sealed partial class CMUNoPermanentDamageSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<CMUNoPermanentDamageComponent, BleedModifierEvent>(OnBleedModifier,
            after: [typeof(StatusEffectsSystem), typeof(AnemicSystem)]);
        SubscribeLocalEvent<CMUNoPermanentDamageComponent, CMBleedAttemptEvent>(OnBleedAttempt);
    }

    private void OnBleedModifier(Entity<CMUNoPermanentDamageComponent> ent, ref BleedModifierEvent args)
    {
        args.BleedAmount = 0;
    }

    private void OnBleedAttempt(Entity<CMUNoPermanentDamageComponent> ent, ref CMBleedAttemptEvent args)
    {
        args.Cancelled = true;
    }
}
