using Content.Shared._RMC14.Targeting;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Weapons.Ranged.Vulture;

// every Vulture shot settles for a moment first, and whoever's under the crosshair gets lasered
// so they get a chance to break line of sight. the shot itself only ever fires on the server
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(CMUVultureWindupSystem))]
public sealed partial class CMUVultureWindupComponent : Component
{
    [DataField]
    public TimeSpan WindupTime = TimeSpan.FromSeconds(1.5);

    // how far from the aim point a mob can be and still get marked
    [DataField]
    public float MarkRadius = 1f;

    [DataField]
    public TargetedEffects MarkEffect = TargetedEffects.Targeted;

    // without the bipod down the shot gets thrown up to this many degrees either way
    [DataField]
    public float UnbracedSpreadDegrees = 15f;

    [DataField]
    public string BipodSlot = "rmc-aslot-underbarrel";

    [DataField]
    public SoundSpecifier WindupSound = new SoundPathSpecifier("/Audio/_RMC14/Weapons/Handling/target_on.ogg");

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? WindupEndsAt;

    public EntityUid? User;

    public EntityCoordinates? AimCoordinates;

    public MapCoordinates? UserOrigin;

    public EntityUid? MarkedTarget;

    // set only while the server is firing the finished shot, so that one attempt goes through
    public bool Releasing;
}
