using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Traits.Phobias;

/// <summary>
///     Fear of heights: standing within a couple of tiles of a drop more than one story deep makes you dizzy.
/// </summary>
[RegisterComponent]
public sealed partial class CMUFearOfHeightsComponent : Component
{
    /// <summary>How close to the edge (in tiles) it gets to you.</summary>
    [DataField]
    public int Range = 2;

    [DataField]
    public TimeSpan Dizziness = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan MessageCooldown = TimeSpan.FromSeconds(25);

    public TimeSpan NextCheck;
    public TimeSpan NextMessage;
    public bool WasScared;
}

/// <summary>
///     Fear of water: standing in water makes you dizzy.
/// </summary>
[RegisterComponent]
public sealed partial class CMUFearOfWaterComponent : Component
{
    [DataField]
    public TimeSpan Dizziness = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan MessageCooldown = TimeSpan.FromSeconds(25);

    public TimeSpan NextCheck;
    public TimeSpan NextMessage;
    public bool WasScared;
}

public enum CMUSwimWater : byte
{
    None,
    Shallow,
    Deep,
}

/// <summary>
///     Can't swim: even slower in water than everyone else, much slower in deep water, and after a few seconds in deep
///     water they start flailing and drowning.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
public sealed partial class CMUCantSwimComponent : Component
{
    [DataField, AutoNetworkedField]
    public CMUSwimWater Water;

    [DataField]
    public float ShallowSpeed = 0.8f;

    [DataField]
    public float DeepSpeed = 0.55f;

    /// <summary>Water at least this deep is over their head.</summary>
    [DataField]
    public float DeepDepth = 14f;

    /// <summary>How long in deep water before they go under.</summary>
    [DataField]
    public TimeSpan DrownAfter = TimeSpan.FromSeconds(4);

    [DataField]
    public float DrownDamage = 4f;

    [DataField]
    public TimeSpan FlailMessageCooldown = TimeSpan.FromSeconds(4);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan DeepSince;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextCheck;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextDrown;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextFlailMessage;

    public bool Drowning;
}
