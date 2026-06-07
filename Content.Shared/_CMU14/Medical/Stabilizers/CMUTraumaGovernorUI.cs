using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Stabilizers;

[Serializable, NetSerializable]
public enum CMUTraumaGovernorUI : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class CMUTraumaGovernorChooseOrganBuiMsg(CMUOrganStabilizerTarget target) : BoundUserInterfaceMessage
{
    public readonly CMUOrganStabilizerTarget Target = target;
}

