using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Injuries.Wounds;

[Serializable, NetSerializable]
public enum WoundCategory : byte
{
    Cut = 0,
    Bruise,
    Burn,
    InternalBleeding,
    LostLimb,
}

[Serializable, NetSerializable]
public enum WoundSize : byte
{
    CutSmall = 0,
    CutDeep,
    CutFlesh,
    CutGaping,
    CutGapingBig,
    CutMassive,
    Bruise,
    BurnModerate,
    BurnLarge,
    BurnSevere,
    BurnDeep,
    BurnCarbonised,
    InternalBleeding,
    LostLimbSmall,
    LostLimb,
}
