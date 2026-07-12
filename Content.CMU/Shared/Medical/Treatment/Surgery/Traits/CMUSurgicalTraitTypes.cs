namespace Content.Shared._CMU14.Medical.Treatment.Surgery.Traits;

public enum CMUSurgicalTrait : byte
{
    VascularTear,
    EmbeddedForeignBody,
    CompartmentPressure,
    ContaminatedWound,
    BoneSplintered,
    OrganAdhesion,
    OrganHemorrhage,
}

public static class CMUSurgicalTraitMetadata
{
    public const int MaxGeneratedTraitsPerPart = 2;

    public static readonly CMUSurgicalTrait[] ResolutionOrder =
    {
        CMUSurgicalTrait.VascularTear,
        CMUSurgicalTrait.EmbeddedForeignBody,
        CMUSurgicalTrait.CompartmentPressure,
        CMUSurgicalTrait.ContaminatedWound,
        CMUSurgicalTrait.BoneSplintered,
        CMUSurgicalTrait.OrganAdhesion,
        CMUSurgicalTrait.OrganHemorrhage,
    };

    public static string ConditionLocId(CMUSurgicalTrait trait)
    {
        return trait switch
        {
            CMUSurgicalTrait.VascularTear => "cmu-medical-surgery-condition-vascular-tear",
            CMUSurgicalTrait.EmbeddedForeignBody => "cmu-medical-surgery-condition-embedded-foreign-body",
            CMUSurgicalTrait.CompartmentPressure => "cmu-medical-surgery-condition-compartment-pressure",
            CMUSurgicalTrait.ContaminatedWound => "cmu-medical-surgery-condition-contaminated-wound",
            CMUSurgicalTrait.BoneSplintered => "cmu-medical-surgery-condition-bone-splinters",
            CMUSurgicalTrait.OrganAdhesion => "cmu-medical-surgery-condition-organ-adhesion",
            CMUSurgicalTrait.OrganHemorrhage => "cmu-medical-surgery-condition-organ-hemorrhage",
            _ => "cmu-medical-surgery-condition-in-progress",
        };
    }
}
