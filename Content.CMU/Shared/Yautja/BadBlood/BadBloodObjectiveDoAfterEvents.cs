using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Yautja.BadBlood;

// CMU14: the marine objective chain against the Bad Blood clan (docs/GraphAudit-BadBlood-ObjectivesPlan.md ARCH-504,
// FLOW-508, FLOW-511). The systems that handle these are server-only; the events are here so they can be networked.

/// <summary>
/// A marine finished the uplink at a working uplink site.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class BadBloodUplinkDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
/// A marine finished surveying the revealed Lodge for the Lodge signature.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class BadBloodLodgeSurveyDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
/// One tool step of the bracer signature at an uplink site. <see cref="Stage"/> is the site's bracer stage the step
/// started from, so a step that finishes after the site moved on does nothing.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class BadBloodBracerTinkerDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public int Stage;
}

/// <summary>
/// A marine finished planting a field uplink relay kit.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class BadBloodRelayPlantDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
/// A marine finished surveying a desecrated Clan Totem for the Totem signature.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class BadBloodTotemSurveyDoAfterEvent : SimpleDoAfterEvent;
