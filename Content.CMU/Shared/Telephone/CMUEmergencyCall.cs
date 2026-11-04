using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Telephone;

[Serializable, NetSerializable]
public enum CMUEmergencyService : byte
{
    FireMedical,
    LawEnforcement,
}

[Serializable, NetSerializable]
public sealed class CMUEmergencyCallBuiMsg(CMUEmergencyService service) : BoundUserInterfaceMessage
{
    public readonly CMUEmergencyService Service = service;
}
