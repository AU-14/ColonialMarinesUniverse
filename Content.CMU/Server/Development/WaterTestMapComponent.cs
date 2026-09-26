namespace Content.Server.CMU14.Development;

/// <summary>Opt-in automatic player spawning for the water effects test arena.</summary>
[RegisterComponent]
public sealed partial class WaterTestMapComponent : Component;

/// <summary>Initial pose for a specimen placed in the water effects test arena.</summary>
[RegisterComponent]
public sealed partial class WaterTestSubjectComponent : Component
{
    [DataField]
    public bool Resting;

    [DataField]
    public bool Dead;
}
