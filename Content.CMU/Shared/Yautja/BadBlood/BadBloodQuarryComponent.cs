using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// FLOW-512: the Lodge quarry part. It holds the clan's stone: Bad Bloods draw it as bricks and build fortifications
/// with them near the Lodge or a Totem. Offerings, Totem income and sabotage add to it, up to <see cref="StoneCap"/>.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodQuarryComponent : Component
{
    public const float DefaultBuildRadius = 12f;

    /// <summary>
    /// Stone the quarry holds when the Lodge is pitched.
    /// </summary>
    [DataField]
    public int StartStone = 20;

    [DataField]
    public int StoneCap = 40;

    /// <summary>
    /// Bricks one draw hands out, at most.
    /// </summary>
    [DataField]
    public int DrawAmount = 10;

    /// <summary>
    /// Stone added for each trophy offering credited at the Lodge.
    /// </summary>
    [DataField]
    public int OfferingStone = 5;

    /// <summary>
    /// Stone added per whole Superiority Point a Totem pays.
    /// </summary>
    [DataField]
    public int TotemStonePerSp = 1;

    /// <summary>
    /// Stone added for each marine objective site a Bad Blood sabotages.
    /// </summary>
    [DataField]
    public int SabotageStone = 3;

    /// <summary>
    /// Bricks only build within this many tiles of the standing Lodge or a registered, undesecrated Totem.
    /// </summary>
    [DataField]
    public float BuildRadius = DefaultBuildRadius;

    /// <summary>
    /// The brick stack a draw spawns.
    /// </summary>
    [DataField]
    public EntProtoId BrickPrototype = "CMUSheetBadBloodBrick1";

    [ViewVariables]
    public int Stone;
}
