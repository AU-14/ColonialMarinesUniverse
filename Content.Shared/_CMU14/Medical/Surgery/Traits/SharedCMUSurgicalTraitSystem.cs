using System.Collections.Generic;
using Robust.Shared.GameObjects;

namespace Content.Shared._CMU14.Medical.Surgery.Traits;

public sealed partial class SharedCMUSurgicalTraitSystem : EntitySystem
{
    public bool HasTrait(EntityUid part, CMUSurgicalTrait trait)
    {
        return trait switch
        {
            CMUSurgicalTrait.VascularTear => HasComp<CMUVascularTearComponent>(part),
            CMUSurgicalTrait.EmbeddedForeignBody => HasComp<CMUEmbeddedForeignBodyComponent>(part),
            CMUSurgicalTrait.CompartmentPressure => HasComp<CMUCompartmentPressureComponent>(part),
            CMUSurgicalTrait.ContaminatedWound => HasComp<CMUContaminatedWoundComponent>(part),
            CMUSurgicalTrait.BoneSplintered => HasComp<CMUBoneSplinteredComponent>(part),
            CMUSurgicalTrait.OrganAdhesion => HasComp<CMUOrganAdhesionComponent>(part),
            CMUSurgicalTrait.OrganHemorrhage => HasComp<CMUOrganHemorrhageComponent>(part),
            _ => false,
        };
    }

    public bool TryEnsureTrait(
        EntityUid part,
        CMUSurgicalTrait trait,
        int maxTraits = CMUSurgicalTraitMetadata.MaxGeneratedTraitsPerPart)
    {
        if (HasTrait(part, trait))
            return false;

        if (CountTraits(part) >= maxTraits)
            return false;

        EnsureTrait(part, trait);
        return true;
    }

    public void EnsureTrait(EntityUid part, CMUSurgicalTrait trait)
    {
        switch (trait)
        {
            case CMUSurgicalTrait.VascularTear:
                EnsureComp<CMUVascularTearComponent>(part);
                break;
            case CMUSurgicalTrait.EmbeddedForeignBody:
                EnsureComp<CMUEmbeddedForeignBodyComponent>(part);
                break;
            case CMUSurgicalTrait.CompartmentPressure:
                EnsureComp<CMUCompartmentPressureComponent>(part);
                break;
            case CMUSurgicalTrait.ContaminatedWound:
                EnsureComp<CMUContaminatedWoundComponent>(part);
                break;
            case CMUSurgicalTrait.BoneSplintered:
                EnsureComp<CMUBoneSplinteredComponent>(part);
                break;
            case CMUSurgicalTrait.OrganAdhesion:
                EnsureComp<CMUOrganAdhesionComponent>(part);
                break;
            case CMUSurgicalTrait.OrganHemorrhage:
                EnsureComp<CMUOrganHemorrhageComponent>(part);
                break;
        }
    }

    public bool RemoveTrait(EntityUid part, CMUSurgicalTrait trait)
    {
        if (!HasTrait(part, trait))
            return false;

        switch (trait)
        {
            case CMUSurgicalTrait.VascularTear:
                RemComp<CMUVascularTearComponent>(part);
                break;
            case CMUSurgicalTrait.EmbeddedForeignBody:
                RemComp<CMUEmbeddedForeignBodyComponent>(part);
                break;
            case CMUSurgicalTrait.CompartmentPressure:
                RemComp<CMUCompartmentPressureComponent>(part);
                break;
            case CMUSurgicalTrait.ContaminatedWound:
                RemComp<CMUContaminatedWoundComponent>(part);
                break;
            case CMUSurgicalTrait.BoneSplintered:
                RemComp<CMUBoneSplinteredComponent>(part);
                break;
            case CMUSurgicalTrait.OrganAdhesion:
                RemComp<CMUOrganAdhesionComponent>(part);
                break;
            case CMUSurgicalTrait.OrganHemorrhage:
                RemComp<CMUOrganHemorrhageComponent>(part);
                break;
        }

        return true;
    }

    public int CountTraits(EntityUid part)
    {
        var count = 0;
        foreach (var trait in CMUSurgicalTraitMetadata.ResolutionOrder)
        {
            if (HasTrait(part, trait))
                count++;
        }

        return count;
    }

    public IEnumerable<CMUSurgicalTrait> EnumerateOrderedTraits(EntityUid part)
    {
        foreach (var trait in CMUSurgicalTraitMetadata.ResolutionOrder)
        {
            if (HasTrait(part, trait))
                yield return trait;
        }
    }
}
