using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Medical.Blood;

/// <summary>A mob's blood type. Taken from the character profile, or rolled when there isn't one.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUBloodTypeComponent : Component
{
    // "type" is taken by the component's own YAML key.
    [DataField("bloodType"), AutoNetworkedField]
    public CMUBloodType Type = CMUBloodType.OPositive;
}

/// <summary>
/// The blood type of the blood in a blood pack, shown on its label. Null on an empty pack. A pack filled from two
/// different types is mixed, and is safe for nobody.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CMUBloodPackTypeComponent : Component
{
    // "type" is taken by the component's own YAML key.
    [DataField("bloodType"), AutoNetworkedField]
    public CMUBloodType? Type;

    [DataField, AutoNetworkedField]
    public bool Mixed;
}

/// <summary>Makes an IV or blood pack holding this pack flow faster (plasma runs in quicker than whole blood).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUIVFlowComponent : Component
{
    [DataField]
    public float Multiplier = 1f;
}

/// <summary>
/// A blood pack being squeezed by hand. While <see cref="Squeezer"/> keeps holding it they squeeze it every <see cref="Interval"/>,
/// and it flows faster until <see cref="Until"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUBloodPackSqueezeComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Squeezer;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan Until;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextSqueeze;

    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(3);

    [DataField]
    public float Multiplier = 3f;
}

/// <summary>A pressure bag that can be fitted to an IV stand to make it flow faster.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUIVPressureBagComponent : Component
{
    [DataField]
    public float Multiplier = 2f;
}

/// <summary>On an IV stand with a pressure bag fitted.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUIVPressureBagFittedComponent : Component
{
    public const string ContainerId = "cmu_pressure_bag";

    [DataField, AutoNetworkedField]
    public float Multiplier = 2f;
}

/// <summary>Recent plasma transfusion: bleeding is slowed until <see cref="Until"/>.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUPlasmaClottingComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan Until;

    /// <summary>How much of normal bleeding still gets through.</summary>
    [DataField]
    public float BleedMultiplier = 0.5f;
}

/// <summary>How much of a patient's blood volume is really saline from a saline drip.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUSalineDilutionComponent : Component
{
    [DataField, AutoNetworkedField]
    public FixedPoint2 Saline;
}

/// <summary>
/// Blood or plasma that goes bad after <see cref="ShelfLife"/> outside refrigeration. Time spent somewhere with
/// <see cref="CMUBloodRefrigeratorComponent"/> doesn't count.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUPerishableBloodComponent : Component
{
    [DataField]
    public TimeSpan ShelfLife = TimeSpan.FromMinutes(30);

    /// <summary>Unrefrigerated time spent so far.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan Warm;

    [DataField, AutoNetworkedField]
    public bool Spoiled;
}

/// <summary>Keeps blood and plasma stored inside it from going bad, like an IV stand or a freezer crate.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUBloodRefrigeratorComponent : Component;
