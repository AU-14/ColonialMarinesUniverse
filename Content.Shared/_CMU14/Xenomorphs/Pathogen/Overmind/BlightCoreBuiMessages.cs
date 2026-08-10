using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Xenomorphs.Pathogen.Overmind;

[Serializable, NetSerializable]
public sealed class BlightCoreAcceptMessage : BoundUserInterfaceMessage
{
    public NetEntity Candidate;
}

[Serializable, NetSerializable]
public sealed class BlightCoreDeclineMessage : BoundUserInterfaceMessage
{
}