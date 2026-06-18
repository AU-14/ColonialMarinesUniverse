using Content.Server.Body.Systems;
using Content.Shared._CMU14.Medical.Chemistry;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Effects;
using Content.Shared._CMU14.Medical.Human.Surgery;
using Content.Shared._CMU14.Medical.Presentation;
using Content.Shared._RMC14.Humanoid;
using Content.Shared._RMC14.Medical.HUD.Components;
using Content.Shared._RMC14.Synth;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Humanoid;

namespace Content.Server._RMC14.Synth;

public sealed partial class SynthSystem : SharedSynthSystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private SharedBodySystem _body = default!;

    protected override void MakeSynth(Entity<SynthComponent> ent)
    {
        var wasInitialized = ent.Comp.Initialized;
        base.MakeSynth(ent);

        if (TryComp<DamageableComponent>(ent.Owner, out var damageable))
            _damageable.SetDamageModifierSetId(ent.Owner, ent.Comp.NewDamageModifier, damageable);

        if (TryComp<BloodstreamComponent>(ent.Owner, out var bloodstream)) // These TryComps are so tests don't fail
        {
            // This makes it so the synth doesn't take bloodloss damage.
            _bloodstream.SetBloodLossThreshold((ent, bloodstream), 0f);
            _bloodstream.ChangeBloodReagent((ent, bloodstream), ent.Comp.NewBloodReagent);
        }

        // CMU14 start - synths use RMC synthetic damage/repair, never the human ledger.
        RemComp<HumanMedicalComponent>(ent.Owner);
        RemComp<HumanMedicalSummaryComponent>(ent.Owner);
        RemComp<HumanMedicalVisualsComponent>(ent.Owner);
        RemComp<ActiveBleedingComponent>(ent.Owner);
        RemComp<ActiveOrganSymptomsComponent>(ent.Owner);
        RemComp<ActiveBoneKnittingComponent>(ent.Owner);
        RemComp<ActiveUnsplintedFractureRiskComponent>(ent.Owner);
        RemComp<ActiveEmbeddedObjectMovementComponent>(ent.Owner);
        RemComp<ActiveTourniquetComponent>(ent.Owner);
        RemComp<ActiveTreatedWoundHealingComponent>(ent.Owner);
        RemComp<ActiveMedicalSummaryDirtyComponent>(ent.Owner);
        RemComp<ActiveHumanSurgeryOperationComponent>(ent.Owner);
        RemComp<HumanChemicalOrganStasisComponent>(ent.Owner);
        RemComp<PainShockComponent>(ent.Owner);
        RemComp<CMUPainFeedbackComponent>(ent.Owner);
        RemComp<ActivePainFeedbackComponent>(ent.Owner);
        // CMU14 end

        if (wasInitialized)
            return;

        // Remove DNA and Fingerprint components if present
        RemComp<Content.Shared.Forensics.Components.DnaComponent>(ent.Owner);
        RemComp<Content.Shared.Forensics.Components.FingerprintComponent>(ent.Owner);

        var repOverrideComp = EnsureComp<RMCHumanoidRepresentationOverrideComponent>(ent);
        if (!ent.Comp.HideGeneration)
        {
            repOverrideComp.Age = ent.Comp.Generation;
            repOverrideComp.Species = ent.Comp.SpeciesName;

        }
        Dirty(ent, repOverrideComp);

        // If UseHumanHealthIcons is true, use the same health icons as a human
        if (ent.Comp.UseHumanHealthIcons)
        {
            ent.Comp.HealthIconOverrides = new()
            {
                [RMCHealthIconTypes.Healthy] = "CMHealthIconHealthy",
                [RMCHealthIconTypes.DeadDefib] = "CMHealthIconDeadDefib",
                [RMCHealthIconTypes.DeadClose] = "CMHealthIconDeadClose",
                [RMCHealthIconTypes.DeadAlmost] = "CMHealthIconDeadAlmost",
                [RMCHealthIconTypes.DeadDNR] = "CMHealthIconDeadDNR",
                [RMCHealthIconTypes.Dead] = "CMHealthIconDead",
                [RMCHealthIconTypes.HCDead] = "CMHealthIconHCDead",
            };
        }

        if (!TryComp<BodyComponent>(ent.Owner, out var body))
            return;

        var organComps = _body.GetBodyOrganEntityComps<OrganComponent>((ent.Owner, body));

        foreach (var organ in organComps)
        {
            Del(organ); // Synths do not metabolize chems or breathe
        }

        var headSlots = _body.GetBodyChildrenOfType(ent, BodyPartType.Head);

        foreach (var part in headSlots)
        {
            if (!ent.Comp.ChangeBrain)
                return;
            var newBrain = SpawnNextToOrDrop(ent.Comp.NewBrain, ent);
            _body.AddOrganToFirstValidSlot(part.Id, newBrain);
            break;
        }
    }
}
