using System.Collections.Generic;
using Content.Shared.Body.Part;
using Content.Shared._CMU14.Medical.Human.Data;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Targeting;

[Serializable, NetSerializable]
public enum BodyPartPickerUIKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum BodyPartPickerRadialStatus : byte
{
    Uninjured = 0,
    Brute,
    Burn,
    Both,
    Surgery,
}

[Serializable, NetSerializable]
public sealed class BodyPartPickerBuiState : BoundUserInterfaceState
{
    public readonly NetEntity Patient;
    public readonly List<BodyPartPickerEntry> Available;

    public BodyPartPickerBuiState(NetEntity patient, List<BodyPartPickerEntry> available)
    {
        Patient = patient;
        Available = available;
    }
}

[Serializable, NetSerializable]
public readonly record struct BodyPartPickerEntry(
    NetEntity Part,
    BodyPartType Type,
    BodyPartSymmetry Symmetry,
    int UntreatedWounds,
    string DisplayName,
    BodyRegion Region,
    BodyPartPickerRadialStatus RadialStatus);

[Serializable, NetSerializable]
public sealed class BodyPartPickerSelectMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity Part;
    public readonly BodyRegion Region;

    public BodyPartPickerSelectMessage(NetEntity part, BodyRegion region)
    {
        Part = part;
        Region = region;
    }
}
