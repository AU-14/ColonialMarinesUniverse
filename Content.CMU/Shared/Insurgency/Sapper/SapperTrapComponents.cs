using Content.Shared.DoAfter;
using Content.Shared.Tools;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._AU14.Insurgency.Sapper;

/// <summary>
///     A CLF Sapper trap: an item that is deployed onto a tile where it hides itself, arms after a short
///     delay, and then fires (via the entity's own trigger components, e.g. an explosion on step) when a
///     non-CLF mob steps on it. CLF members are immune, so the cell can walk over its own field freely.
///
///     This component only owns the placement, arming, hiding, and friendly-gating behavior. What the trap
///     actually does when it goes off is left to the ordinary trigger components on the prototype
///     (TriggerOnStepTrigger + ExplodeOnTrigger, a flash, a spawn, and so on), so new trap types are just
///     new prototypes with this component plus whatever payload the author wants.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SapperTrapComponent : Component
{
    // ---------------------------------------------------------------------
    // Tunables. One place to tweak how every sapper trap places, arms, hides,
    // and disarms. Individual prototypes can override any of these.
    // ---------------------------------------------------------------------

    /// <summary>
    ///     How long the deploy do-after takes before the trap is planted.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DeployTime = 4f;

    /// <summary>
    ///     Spawned at the trap's future spot for the duration of the deploy do-after, showing a faint hue
    ///     over the area the trigger will cover. Null for wire traps, whose covered area is only known once
    ///     the wire is strung.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId? DeployPreviewPrototype = "AU14SapperTrapAreaPreview";

    /// <summary>
    ///     Effect spawned on the victim when the trap goes off.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId? TripEffect = "EffectSparks";

    /// <summary>
    ///     Sharp sound played at the victim when the trap goes off, on top of whatever the trap's own
    ///     payload sounds like.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier? TripSound = new SoundPathSpecifier("/Audio/Effects/snap.ogg");

    /// <summary>
    ///     How long it takes an enemy to disarm the trap with the right tool.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DisarmTime = 4f;

    /// <summary>
    ///     Tool quality that disarms the trap (wirecutters by default, so a GOVFOR engineer can clear it).
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<ToolQualityPrototype> DisarmTool = "Cutting";

    /// <summary>
    ///     After being planted the trap needs this long before it can fire, so a fumbled deploy can't blow
    ///     up the sapper who just placed it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ArmingDelay = 3f;

    /// <summary>
    ///     An enemy viewer within this many tiles sees the trap fully - for that viewer only (decided
    ///     client-side, per viewer, in SapperTrapVisualsSystem). CLF members always see their own traps
    ///     regardless of range.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float RevealRadius = 1.75f;

    /// <summary>
    ///     Sprite alpha an enemy sees while the trap is still hidden. Not fully invisible: a sharp eye can
    ///     just barely make it out from a distance. 1 is fully opaque.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float HiddenAlpha = 0.12f;

    // ---------------------------------------------------------------------
    // Runtime state.
    // ---------------------------------------------------------------------

    /// <summary>
    ///     Whether the trap has been planted (anchored on a tile). A carried, un-planted trap never fires.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Deployed;

    /// <summary>
    ///     Whether the trap is live. Set once the arming delay elapses; only an armed trap can be tripped.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Armed;

    /// <summary>
    ///     When the trap becomes armed. Null once armed or before it is planted.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan? ArmsAt;

    /// <summary>
    ///     The live trigger-area preview entity while a deploy do-after is running. Server-only bookkeeping.
    /// </summary>
    [ViewVariables]
    public EntityUid? DeployPreview;
}

/// <summary>
///     The "device" half of a two-part tripwire trap. When planted it strings a near-invisible wire
///     forward from itself to an anchor a few tiles away (the "wire end"). Anything that isn't a friendly
///     crossing any part of that wire sets the whole thing off.
///
///     The device itself carries no blast: its punch is entirely the explosives the sapper attaches to it
///     (grenades, mines, C4 - anything with an Explosive component). Everything attached detonates together
///     when the wire is crossed.
///
///     The device reuses <see cref="SapperTrapComponent"/> for its plant / arm / hide / disarm lifecycle;
///     this component only adds the wire and the attached-payload behaviour on top.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SapperTripwireComponent : Component
{
    // ---------------------------------------------------------------------
    // Tunables.
    // ---------------------------------------------------------------------

    /// <summary>Furthest, in tiles, the second end may be planted from the device when running the wire.</summary>
    [DataField]
    public int MaxWireRange = 11;

    /// <summary>The most explosives that can be lashed to one device.</summary>
    [DataField]
    public int MaxPayload = 6;

    /// <summary>Spawned on each tile of the wire between the device and its end; carries the trip trigger.</summary>
    [DataField]
    public EntProtoId SegmentPrototype = "AU14SapperTripwireSegment";

    /// <summary>Spawned on the far tile as the visible anchor the wire is tied off to.</summary>
    [DataField]
    public EntProtoId EndPrototype = "AU14SapperTripwireEnd";

    /// <summary>The "other end" item handed to the sapper after planting, used to run the wire to its far point.</summary>
    [DataField]
    public EntProtoId EndPlacerPrototype = "AU14SapperTripwireEndPlacer";

    /// <summary>Container id the attached explosives live in.</summary>
    [DataField]
    public string PayloadContainer = "tripwire_payload";

    // ---------------------------------------------------------------------
    // Runtime (server-owned; the wire pieces are ordinary spawned entities).
    // ---------------------------------------------------------------------

    [ViewVariables]
    public EntityUid? WireEnd;

    [ViewVariables]
    public List<EntityUid> Segments = new();

    /// <summary>The "other end" placer item handed out on plant, tracked so it can be cleaned up with the device.</summary>
    [ViewVariables]
    public EntityUid? PendingPlacer;
}

/// <summary>
///     The "other end" of a two-part tripwire, handed to the sapper the moment they plant the charge. They
///     carry it to where they want the wire to run and use it in hand there; the tripwire system then strings
///     the wire from the charge to that spot (line of sight and range permitting) and consumes this item.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SapperTripwireEndPlacerComponent : Component
{
    /// <summary>The planted tripwire charge this end belongs to.</summary>
    [ViewVariables]
    public EntityUid Device;
}

/// <summary>
///     One tile of a tripwire's strung wire (or its far anchor end). Purely a link back to the device it
///     belongs to: when a non-friendly steps across this piece it tells the device to detonate. Spawned and
///     cleaned up by the tripwire system; never placed by hand.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SapperTripwireSegmentComponent : Component
{
    /// <summary>The tripwire device this wire piece reports back to.</summary>
    [ViewVariables]
    public EntityUid Device;
}

/// <summary>
///     Turns a two-part wire trap into an audio (early-warning) trap. Set up exactly like the tripwire
///     charge - plant, carry the other end, string the wire - but it needs no explosives attached. When
///     something crosses the wire it blasts a loud whistle at the spot and reports the crossing, with the
///     name the sapper gave it and its area, over the CLF radio channel (mirroring how xeno resin traps
///     report to the hivemind). It stays live afterwards, re-alerting after a short cooldown.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SapperAudioTrapComponent : Component
{
    // ---------------------------------------------------------------------
    // Tunables.
    // ---------------------------------------------------------------------

    /// <summary>The alarm blown at the wire when it is crossed. Loud on purpose.</summary>
    [DataField]
    public SoundSpecifier AlarmSound =
        new SoundPathSpecifier("/Audio/Items/Whistle/trench_whistle_1.ogg", AudioParams.Default.WithVolume(8f).WithMaxDistance(24f));

    /// <summary>Minimum time between alerts so a squad walking the wire doesn't spam the radio.</summary>
    [DataField]
    public TimeSpan AlertCooldown = TimeSpan.FromSeconds(10);

    /// <summary>Longest name the sapper may give the trap (untrusted client text, clamped server-side).</summary>
    [DataField]
    public int MaxNameLength = 32;

    // ---------------------------------------------------------------------
    // Runtime.
    // ---------------------------------------------------------------------

    /// <summary>The name the sapper gave this trap when planting it, used in the radio report.</summary>
    [DataField]
    public string TrapName = "";

    [ViewVariables]
    public TimeSpan NextAlert = TimeSpan.Zero;
}

/// <summary>
///     Raised on an audio trap when its wire is crossed. Where is the crossed wire piece's spot, so the
///     whistle blows at the wire rather than at the buried box.
/// </summary>
[ByRefEvent]
public record struct SapperAudioTrapTrippedEvent(EntityUid Tripper, EntityCoordinates Where);

/// <summary>
///     Marks a sapper trap as a snare: instead of a lethal payload it ensnares whoever trips it - binds
///     their hands, flips their view and sprite upside down, and roots them until they struggle free (or a
///     friend cuts them loose). Pair with <see cref="SapperTrapComponent"/> so it still plants, hides, arms,
///     and spares friendlies like any other trap. The values here are copied onto the victim's
///     <see cref="SapperSnaredComponent"/> when it goes off.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SapperSnareComponent : Component
{
    /// <summary>How long the victim must struggle on their own to break free.</summary>
    [DataField]
    public TimeSpan StruggleTime = TimeSpan.FromSeconds(60);

    /// <summary>How long it takes a friend with a knife to cut the victim loose.</summary>
    [DataField]
    public TimeSpan CutFreeTime = TimeSpan.FromSeconds(1);

    /// <summary>How far the victim's view and sprite are rotated while snared (180 = upside down).</summary>
    [DataField]
    public Angle FlipAngle = Angle.FromDegrees(180);

    /// <summary>The cuffs clapped onto the victim when the snare goes off (dropped/removed when they get free).</summary>
    [DataField]
    public EntProtoId CuffPrototype = "Handcuffs";
}

/// <summary>
///     Placed on a victim caught in a snare trap. While present they are rooted, their hands are bound,
///     and their view and sprite are flipped upside down. Removing it (by struggling free or being cut
///     loose) undoes all of that.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SapperSnaredComponent : Component
{
    /// <summary>How long the victim must struggle on their own to break free.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan StruggleTime = TimeSpan.FromSeconds(60);

    /// <summary>How long it takes a friend with a knife to cut the victim loose.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan CutFreeTime = TimeSpan.FromSeconds(1);

    /// <summary>How far the victim's view and sprite are rotated (180 = upside down).</summary>
    [DataField, AutoNetworkedField]
    public Angle FlipAngle = Angle.FromDegrees(180);

    /// <summary>The cuffs clapped on the victim, tracked so they can be removed when the snare ends. Server-only.</summary>
    [ViewVariables]
    public EntityUid? Cuffs;
}

/// <summary>DoAfter for the victim struggling out of a snare on their own.</summary>
[Serializable, NetSerializable]
public sealed partial class SapperStruggleDoAfterEvent : SimpleDoAfterEvent
{
}

/// <summary>DoAfter for a friend cutting a snared victim loose with a knife.</summary>
[Serializable, NetSerializable]
public sealed partial class SapperCutFreeDoAfterEvent : SimpleDoAfterEvent
{
}
