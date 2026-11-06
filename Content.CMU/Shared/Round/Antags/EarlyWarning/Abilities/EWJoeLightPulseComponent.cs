using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;

/// <summary>
/// THE LIGHT ripples. Granted by the level-5 table beside the Joe's <c>PointLight</c>; every client animates that
/// light from these values, a soft orange glow that swells and ebbs in overlapping waves like light thrown off
/// moving water. Presence-only on the network (the values are code defaults): by level 5 APOLLO has named the
/// threat and the light itself gives the Joe away, so the marker reveals nothing new.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class EWJoeLightPulseComponent : Component
{
    /// <summary>Seconds per long swell; shorter crossing waves ride on top of it.</summary>
    [DataField]
    public float Period = 2.6f;

    [DataField]
    public float MinEnergy = 1.8f;

    [DataField]
    public float MaxEnergy = 2.6f;

    [DataField]
    public float MinRadius = 8f;

    [DataField]
    public float MaxRadius = 9.5f;

    /// <summary>The trough of a wave: a deeper orange.</summary>
    [DataField]
    public Color Deep = Color.FromHex("#ff7000");

    /// <summary>The crest of a wave: a lighter orange.</summary>
    [DataField]
    public Color Bright = Color.FromHex("#ffa030");
}
