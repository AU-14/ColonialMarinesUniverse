using Content.Shared._RMC14.Medical.Wounds;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Injuries.Wounds;

/// <summary>
///     One coherent wound-ledger row. All treatment metadata travels with the
///     wound it describes.
/// </summary>
[DataRecord]
[Serializable]
public partial record struct CMUWoundEntry
{
    public CMUWoundEntry()
    {
    }

    public Wound Wound { get; set; }
    public WoundSize Size { get; set; }
    public int Bandages { get; set; }
    public WoundMechanism Mechanism { get; set; }
    public WoundMechanismFlags SecondaryMechanisms { get; set; }
    public WoundTreatmentQuality TreatmentQuality { get; set; }
    public WoundCleanupFlags Cleanup { get; set; }

    public CMUWoundEntry(
        Wound wound,
        WoundSize size,
        int bandages,
        WoundMechanism mechanism,
        WoundMechanismFlags secondaryMechanisms,
        WoundTreatmentQuality treatmentQuality,
        WoundCleanupFlags cleanup)
    {
        Wound = wound;
        Size = size;
        Bandages = bandages;
        Mechanism = mechanism;
        SecondaryMechanisms = secondaryMechanisms;
        TreatmentQuality = treatmentQuality;
        Cleanup = cleanup;
    }
}
