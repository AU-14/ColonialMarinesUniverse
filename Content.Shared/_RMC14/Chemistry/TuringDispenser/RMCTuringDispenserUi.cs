using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Chemistry.TuringDispenser;

[Serializable, NetSerializable]
public enum RMCTuringDispenserUi
{
    Key,
}

[Serializable, NetSerializable]
public enum TuringDispenserProgram : byte
{
    Memory,
    Box,
}

[Serializable, NetSerializable]
public enum TuringDispenserStatus : byte
{
    Idle,
    Running,
    Finished,
    Stuck,
}

[Serializable, NetSerializable]
public enum TuringDispenserOutputMode : byte
{
    Container,
    SmartFridge,
    Centrifuge,
}

[DataRecord]
[Serializable, NetSerializable]
public readonly partial record struct TuringProgramEntry(ProtoId<ReagentPrototype> Reagent, FixedPoint2 Amount);

[DataRecord]
[Serializable, NetSerializable]
public readonly partial record struct TuringBeakerOption(EntProtoId Id, FixedPoint2 MaxVolume, bool VendorStocked);

[Serializable, NetSerializable]
public sealed partial class RMCTuringDispenserRunProgramBuiMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed partial class RMCTuringDispenserSaveToMemoryBuiMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed partial class RMCTuringDispenserClearMemoryBuiMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed partial class RMCTuringDispenserEjectBoxBuiMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed partial class RMCTuringDispenserEjectBeakerBuiMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed partial class RMCTuringDispenserDisposeBeakerBuiMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed partial class RMCTuringDispenserSetMultiplierBuiMsg(FixedPoint2 multiplier) : BoundUserInterfaceMessage
{
    public readonly FixedPoint2 Multiplier = multiplier;
}

[Serializable, NetSerializable]
public sealed partial class RMCTuringDispenserSetCyclesBuiMsg(int cycles) : BoundUserInterfaceMessage
{
    public readonly int Cycles = cycles;
}

[Serializable, NetSerializable]
public sealed partial class RMCTuringDispenserToggleAutoRunBuiMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed partial class RMCTuringDispenserToggleSmartLinkBuiMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed partial class RMCTuringDispenserToggleOutputModeBuiMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed partial class RMCTuringDispenserSetPreferredBeakerBuiMsg(EntProtoId? beaker) : BoundUserInterfaceMessage
{
    public readonly EntProtoId? Beaker = beaker;
}
