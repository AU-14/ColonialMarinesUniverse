using Content.Shared._RMC14.Marines.Orders;
using Content.Shared._RMC14.Sentry;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Flag;

/// <summary>
///     A deployable flag that rallies nearby allies with marine orders while raised, ported from RMC-14.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(CMUPlantedFlagSystem))]
public sealed partial class CMUPlantedFlagComponent : Component
{
    /// <summary>
    ///     The current mode of the flag.
    /// </summary>
    [DataField, AutoNetworkedField]
    public CMUFlagMode Mode;

    /// <summary>
    ///     The ID used for dropship target selection.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int? Id;

    /// <summary>
    ///     The range in which the buff is applied.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Range = 4.515f; // The result from RMCMathExtensions.CircleAreaFromSquareAbilityRange(3.5)

    /// <summary>
    ///     The strength of the applied buffs.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Strength = 4;

    /// <summary>
    ///     Whether the flag should apply the <see cref="FocusOrderComponent"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ApplyFocus = true;

    /// <summary>
    ///     Whether the flag should apply the <see cref="HoldOrderComponent"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ApplyHold = true;

    /// <summary>
    ///     Whether the flag should apply the <see cref="MoveOrderComponent"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ApplyMove;

    /// <summary>
    ///     The round time at which the flag will apply a buff.
    /// </summary>
    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan NextOrder;

    /// <summary>
    ///     The duration of the buff.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan Duration = TimeSpan.FromSeconds(1.5);

    /// <summary>
    ///     How long it takes before the flag applies a buff again.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(1.5);

    /// <summary>
    ///     The entities the flag is able to upgrade into by using a <see cref="SentryUpgradeItemComponent"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId[]? Upgrades = ["CMUPlantedFlagExtendedRange", "CMUPlantedFlagWarbanner"];

    /// <summary>
    ///     The name of the fixture that is made hard while the flag is deployed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? DeployFixture = "deploy";
}

[Serializable, NetSerializable]
public enum CMUFlagMode
{
    Item,
    Off,
    On,
}

[Serializable, NetSerializable]
public enum CMUFlagLayers
{
    Layer,
}
