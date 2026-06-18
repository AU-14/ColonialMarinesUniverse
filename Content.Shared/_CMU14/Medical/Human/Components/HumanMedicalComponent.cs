using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Human.Data;

namespace Content.Shared._CMU14.Medical.Human.Components;

[RegisterComponent]
public sealed partial class HumanMedicalComponent : Component
{
    public const int MaxInjuriesPerRegion = 16;
    public const int MaxBleedSources = 16;
    public const int MaxForeignObjects = 16;
    public const int MaxForeignObjectFragmentsPerRegion = 12;
    public const int RegionSlotCount = (int) BodyRegion.RightFoot + 1;
    public const int OrganSlotCount = (int) OrganSlot.Ears + 1;

    [DataField]
    public RegionState[] Regions = new RegionState[RegionSlotCount];

    [DataField]
    public List<InjuryRecord> Injuries = new();

    [DataField]
    public OrganState[] Organs = new OrganState[OrganSlotCount];

    [DataField]
    public List<BleedSource> BleedSources = new();

    [DataField]
    public List<ForeignObjectRecord> ForeignObjects = new();

    [DataField]
    public List<DetachedLimbRecord> DetachedLimbs = new();

    [DataField]
    public MedicalSummary Summary;

    [DataField]
    public MedicalDirtyFlags DirtyFlags;

    [DataField]
    public int Revision;

    [DataField]
    public int NextInjuryId = 1;

    [DataField]
    public int NextBleedSourceId = 1;

    [DataField]
    public int NextForeignObjectId = 1;

    [DataField]
    public int NextDetachedLimbId = 1;
}
