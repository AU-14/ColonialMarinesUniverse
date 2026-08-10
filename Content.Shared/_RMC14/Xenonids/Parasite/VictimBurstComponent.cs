using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._RMC14.Xenonids.Parasite;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedXenoParasiteSystem))]
public sealed partial class VictimBurstComponent : Component
{
    [DataField, AutoNetworkedField]
    public ResPath RsiPath = new("/Textures/_RMC14/Effects/burst.rsi");

    /// <summary>
    ///     RSI used for the Neomorph back-burst variant. Separate file from
    ///     the standard chest-burst RSI.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ResPath BackRsiPath = new("/Textures/_CMU14/Effects/back_burst.rsi");

    [DataField, AutoNetworkedField]
    public string BurstState = "bursted_stand";

    [DataField, AutoNetworkedField]
    public string BurstingState = "burst_stand";

    // Neomorph back-burst variants
    [DataField, AutoNetworkedField]
    public string BackBurstState = "bursted_stand";

    [DataField, AutoNetworkedField]
    public string BackBurstingState = "burst_stand";

    [DataField, AutoNetworkedField]
    public bool BurstsFromBack;
}

[Serializable, NetSerializable]
public enum VictimBurstState : byte
{
    Bursting = 1,
    Burst = 2
}