using Content.Shared._RMC14.Intel;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Vehicle.Supply;

[ByRefEvent]
public readonly record struct CMUIntelTreeCreatedEvent(string Team, IntelTechTree Tree);

[Serializable, NetSerializable, DataDefinition]
public sealed partial class CMUTankRequisitionEvent;
