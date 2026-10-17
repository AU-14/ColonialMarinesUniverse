using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Fighter;

[Serializable, NetSerializable]
public enum FighterGroundState : byte { Airborne, Grounded, TakingOff, Returning, Landing, Crashing, Crashed }

/// <summary>The persistent, supply-lift-delivered airframe and its takeoff site.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class FighterGroundComponent : Component
{
    // Keep sockets, presentation and takeoff clearance in the same proportions.
    // Prototype fixture bounds and sprite scale in ground.yml use these sizes.
    public const float SizeMultiplier = 1.3f;
    public const float SpriteScale = .5f * SizeMultiplier;
    public const float AttachmentScale = .25f * SizeMultiplier;

    [DataField, AutoNetworkedField] public EntityUid? Aircraft;
    // Ground viewers may not receive the separate airborne cockpit map.
    [DataField, AutoNetworkedField] public EntityUid? FrontSeat;
    [DataField, AutoNetworkedField] public EntityUid? RearSeat;
    [DataField, AutoNetworkedField] public EntityUid? Canopy;
    [DataField, AutoNetworkedField] public FighterGroundState State = FighterGroundState.Grounded;
    /// <summary>Missiles installed on successive pylons when this airframe first creates its cockpit.</summary>
    [DataField] public List<EntProtoId> StartingMissiles = [];
    [DataField] public EntProtoId? StartingGauAmmo;
    [DataField] public TimeSpan TakeoffTime = TimeSpan.FromSeconds(8);
    [DataField] public TimeSpan LandingTime = TimeSpan.FromSeconds(6);
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan StartedAt;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan EndsAt;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan TouchdownAt;
    [DataField] public EntityCoordinates? LaunchCoordinates;
    [DataField] public Angle LaunchRotation;
    [DataField, AutoNetworkedField] public EntityUid? TaxiPad;
    [DataField, AutoNetworkedField] public FighterInput TaxiInput;
    [DataField] public float PadSearchRange = 12;
    public EntityUid? TransitionAudio;
    public EntityUid? CockpitTransitionAudio;
    public EntityUid? VtolVisual;
    public bool SwappingSeat;
    public Vector2 TaxiLastPosition;
    public TimeSpan TaxiProgressAt;
}

/// <summary>A deployed fighter staging and recovery point.</summary>
[RegisterComponent]
public sealed partial class FighterLandingPadComponent : Component
{
    [DataField] public float Radius = 3.5f;
}
