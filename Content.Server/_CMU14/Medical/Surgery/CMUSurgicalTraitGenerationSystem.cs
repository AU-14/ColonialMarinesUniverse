using Content.Shared._CMU14.Medical.Bones;
using Content.Shared._CMU14.Medical.Bones.Events;
using Content.Shared._CMU14.Medical.Organs;
using Content.Shared._CMU14.Medical.Organs.Events;
using Content.Shared._CMU14.Medical.Surgery.Traits;
using Content.Shared._CMU14.Medical.Shrapnel;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Random;

namespace Content.Server._CMU14.Medical.Surgery;

public sealed partial class CMUSurgicalTraitGenerationSystem : EntitySystem
{
    public const float CompoundContaminationChance = 0.65f;
    public const float ComminutedSecondTraitChance = 0.5f;
    public const float DamagedOrganComplicationChance = 0.25f;
    public const float FailingOrganComplicationChance = 0.6f;

    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedCMUSurgicalTraitSystem _surgicalTraits = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FractureComponent, FractureSeverityChangedEvent>(OnFractureSeverityChanged);
        SubscribeLocalEvent<OrganStageChangedEvent>(OnOrganStageChanged);
        SubscribeLocalEvent<BodyPartComponent, CMUShrapnelChangedEvent>(OnShrapnelChanged);
    }

    private void OnFractureSeverityChanged(Entity<FractureComponent> ent, ref FractureSeverityChangedEvent args)
    {
        if (args.New == FractureSeverity.Compound)
        {
            if (ShouldSeedCompoundContamination(_random.NextFloat()))
                _surgicalTraits.TryEnsureTrait(args.Part, CMUSurgicalTrait.ContaminatedWound);
            return;
        }

        if (args.New != FractureSeverity.Comminuted)
            return;

        _surgicalTraits.TryEnsureTrait(args.Part, CMUSurgicalTrait.BoneSplintered);

        if (!ShouldSeedComminutedSecondTrait(_random.NextFloat()))
            return;

        if (!TryComp<BodyPartComponent>(args.Part, out var part))
            return;

        var secondTrait = part.PartType is BodyPartType.Arm or BodyPartType.Leg
            ? CMUSurgicalTrait.CompartmentPressure
            : CMUSurgicalTrait.VascularTear;

        _surgicalTraits.TryEnsureTrait(args.Part, secondTrait);
    }

    private void OnOrganStageChanged(ref OrganStageChangedEvent args)
    {
        if (!TryGetContainingPart(args.Body, args.Organ, out var part))
            return;

        switch (args.New)
        {
            case OrganDamageStage.Damaged:
                if (ShouldSeedDamagedOrganComplication(_random.NextFloat()))
                    _surgicalTraits.TryEnsureTrait(part, CMUSurgicalTrait.OrganAdhesion);
                break;
            case OrganDamageStage.Failing:
                if (ShouldSeedFailingOrganComplication(_random.NextFloat()))
                    _surgicalTraits.TryEnsureTrait(part, CMUSurgicalTrait.OrganHemorrhage);
                break;
        }
    }

    private void OnShrapnelChanged(Entity<BodyPartComponent> ent, ref CMUShrapnelChangedEvent args)
    {
        if (args.Removed)
        {
            if (!TryComp<CMUShrapnelComponent>(args.Part, out var shrapnel) || shrapnel.Fragments <= 0)
                _surgicalTraits.RemoveTrait(args.Part, CMUSurgicalTrait.EmbeddedForeignBody);
            return;
        }

        _surgicalTraits.TryEnsureTrait(args.Part, CMUSurgicalTrait.EmbeddedForeignBody);
    }

    private bool TryGetContainingPart(EntityUid body, EntityUid organ, out EntityUid part)
    {
        foreach (var (partUid, _) in _body.GetBodyChildren(body))
        {
            foreach (var (organUid, _) in _body.GetPartOrgans(partUid))
            {
                if (organUid != organ)
                    continue;

                part = partUid;
                return true;
            }
        }

        part = default;
        return false;
    }

    public static bool ShouldSeedCompoundContamination(float roll)
    {
        return roll < CompoundContaminationChance;
    }

    public static bool ShouldSeedComminutedSecondTrait(float roll)
    {
        return roll < ComminutedSecondTraitChance;
    }

    public static bool ShouldSeedDamagedOrganComplication(float roll)
    {
        return roll < DamagedOrganComplicationChance;
    }

    public static bool ShouldSeedFailingOrganComplication(float roll)
    {
        return roll < FailingOrganComplicationChance;
    }
}
