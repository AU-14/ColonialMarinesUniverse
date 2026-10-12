using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Medical.Blood;

/// <summary>A mob's blood type. Taken from the character profile, or rolled when there isn't one.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUBloodTypeComponent : Component
{
    [DataField, AutoNetworkedField]
    public CMUBloodType Type = CMUBloodType.OPositive;
}

/// <summary>
/// The blood type of the blood in a blood pack, shown on its label. Null on an empty pack. A pack filled from two
/// different types is mixed, and is safe for nobody.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CMUBloodPackTypeComponent : Component
{
    [DataField, AutoNetworkedField]
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

/// <summary>A blood pack being squeezed by hand flows faster until <see cref="Until"/>.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUBloodPackSqueezeComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan Until;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(5);

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
