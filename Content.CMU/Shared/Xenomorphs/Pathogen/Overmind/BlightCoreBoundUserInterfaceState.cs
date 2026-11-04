using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Xenomorphs.Pathogen.Overmind;

[Serializable, NetSerializable]
public sealed class BlightCoreBuiState : BoundUserInterfaceState
{
    public NetEntity Core;
    public TimeSpan EndsAt;
}