using Content.Shared._CMU14.Medical.Anatomy.Bones;
using Content.Shared._CMU14.Medical.Anatomy.Bones.Events;
using Content.Shared._CMU14.Medical.Anatomy.Organs;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Treatment.Surgery.Traits;
using Content.Shared._CMU14.Medical.Injuries.Shrapnel;
using Content.Shared.Body.Part;
using Robust.Shared.GameObjects;
using Robust.Shared.Random;

namespace Content.Server._CMU14.Medical.Treatment.Surgery;

public sealed partial class CMUSurgicalTraitGenerationSystem : EntitySystem
{
    public const float CompoundContaminationChance = 0.65f;
    public const float ShatteredSecondTraitChance = 0.5f;
    public const float DamagedOrganComplicationChance = 0.25f;
    public const float FailingOrganComplicationChance = 0.6f;
    private const string EyesOrganSlot = "eyes";

    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
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

        if (args.New != FractureSeverity.Shattered)
            return;

        _surgicalTraits.TryEnsureTrait(args.Part, CMUSurgicalTrait.BoneSplintered);

        if (!ShouldSeedShatteredSecondTrait(_random.NextFloat()))
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
        if (!TryGetContainingPart(args.Body, args.Organ, out var part, out var slotId))
            return;
        if (slotId == EyesOrganSlot)
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

    private bool TryGetContainingPart(EntityUid body, EntityUid organ, out EntityUid part, out string slotId)
    {
        part = default;
        slotId = string.Empty;

        if (!_medicalIndex.TryGetOrganOwner(organ, out var owningBody, out var owningPart)
            || owningBody != body)
        {
            return false;
        }

        foreach (var slot in _medicalIndex.GetOrganSlots(owningPart))
        {
            if (slot.Organ != organ)
                continue;

            part = owningPart;
            slotId = slot.SlotId;
            return true;
        }

        return false;
    }

    public static bool ShouldSeedCompoundContamination(float roll)
    {
        return roll < CompoundContaminationChance;
    }

    public static bool ShouldSeedShatteredSecondTrait(float roll)
    {
        return roll < ShatteredSecondTraitChance;
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
