using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.CMU14.Atmos;

/// <summary>
/// Makes a tile fire creep. Every <see cref="SpreadEvery"/> the fire tries to ignite one
/// adjacent free tile with its own prototype. Children spawn with <see cref="Depth"/> - 1
/// and a depth of 0 never spreads, bounding how far a burn can crawl from where it was lit.
/// </summary>
[RegisterComponent]
public sealed partial class CMUSpreadingFireComponent : Component
{
    /// <summary>
    /// Remaining spread generations. The fire that was lit carries the full depth from
    /// YAML; each child gets one less and stops spreading at 0.
    /// </summary>
    [DataField]
    public int Depth = 3;

    /// <summary>
    /// Time between spread attempts.
    /// </summary>
    [DataField]
    public TimeSpan SpreadEvery = TimeSpan.FromSeconds(8);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextSpread;
}
