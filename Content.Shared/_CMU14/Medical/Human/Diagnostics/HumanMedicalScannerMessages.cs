using System;
using Content.Shared._CMU14.Medical.Human.Data;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Human.Diagnostics;

[Serializable, NetSerializable]
public enum HumanMedicalScannerUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum HumanMedicalScannerResponseKind : byte
{
    NoChange = 0,
    FullLedger,
}

[Serializable, NetSerializable]
public sealed class HumanMedicalScannerSummaryState : BoundUserInterfaceState
{
    public MedicalSummary Summary;

    public HumanMedicalScannerSummaryState(MedicalSummary summary)
    {
        Summary = summary;
    }
}

[Serializable, NetSerializable]
public sealed class HumanMedicalScannerFullLedgerRequestMessage : BoundUserInterfaceMessage
{
    public int KnownRevision;

    public HumanMedicalScannerFullLedgerRequestMessage(int knownRevision)
    {
        KnownRevision = knownRevision;
    }
}

[Serializable, NetSerializable]
public sealed class HumanMedicalScannerLedgerResponseMessage : BoundUserInterfaceMessage
{
    public HumanMedicalScannerResponseKind Kind;
    public int Revision;
    public HumanMedicalLedgerDetail? FullLedger;

    public HumanMedicalScannerLedgerResponseMessage(
        HumanMedicalScannerResponseKind kind,
        int revision,
        HumanMedicalLedgerDetail? fullLedger)
    {
        Kind = kind;
        Revision = revision;
        FullLedger = fullLedger;
    }
}

[Serializable, NetSerializable]
public sealed class HumanMedicalLedgerDetail
{
    public int Revision;
    public MedicalSummary Summary;
    public RegionState[] Regions;
    public InjuryRecord[] Injuries;
    public OrganState[] Organs;
    public BleedSource[] BleedSources;
    public ForeignObjectRecord[] ForeignObjects;
    public DetachedLimbRecord[] DetachedLimbs;

    public HumanMedicalLedgerDetail(
        int revision,
        MedicalSummary summary,
        RegionState[] regions,
        InjuryRecord[] injuries,
        OrganState[] organs,
        BleedSource[] bleedSources,
        ForeignObjectRecord[] foreignObjects,
        DetachedLimbRecord[] detachedLimbs)
    {
        Revision = revision;
        Summary = summary;
        Regions = regions;
        Injuries = injuries;
        Organs = organs;
        BleedSources = bleedSources;
        ForeignObjects = foreignObjects;
        DetachedLimbs = detachedLimbs;
    }
}
