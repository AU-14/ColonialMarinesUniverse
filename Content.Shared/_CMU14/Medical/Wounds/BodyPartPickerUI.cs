using System.Collections.Generic;
using Content.Shared.Body.Part;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Wounds;

[Serializable, NetSerializable]
public enum BodyPartPickerUIKey : byte
{
    Key,
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
    string DisplayName);

[Serializable, NetSerializable]
public sealed class BodyPartPickerSelectMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity Part;

    public BodyPartPickerSelectMessage(NetEntity part)
    {
        Part = part;
    }
}
