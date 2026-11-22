using Content.Shared.CMU14.Round.Antags.StrikeOrganizer;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Round.Antags.StrikeOrganizer;

/// <summary>
/// The round's strike venture. Holds everything that must survive any single
/// petition being burned, confiscated, or lost: the roll, the participant ledger,
/// and the milestone state. Petitions are replaceable tokens pointing here.
/// </summary>
[RegisterComponent]
public sealed partial class StrikeOrganizerRuleComponent : Component
{
    /// <summary>
    /// Grievance LocId of the venture. Rolled at rule start, binding for everyone.
    /// </summary>
    [DataField]
    public string? Grievance;

    [DataField]
    public StrikeStance ClfStance;

    [DataField]
    public StrikeStance GovforStance;

    [DataField]
    public bool VentureRolled;

    /// <summary>
    /// Committed strikers needed for the strike vote to pass.
    /// </summary>
    [DataField]
    public int Goal = 10;

    /// <summary>
    /// Entities of the colonists who signed and joined the strike, in order.
    /// </summary>
    [DataField]
    public List<EntityUid> Participants = new();

    public EntityUid? Organizer;
    public bool FaxedHalf;
    public bool FaxedFull;

    /// <summary>
    /// Mind role granted to signers.
    /// </summary>
    [DataField]
    public EntProtoId ParticipantMindRole = "MindRoleStrikeParticipant";

    /// <summary>
    /// Briefing shown when a signer becomes a strike participant.
    /// </summary>
    [DataField]
    public LocId ParticipantBriefing = "cmu-strike-participant-briefing";

    [DataField]
    public Color BriefingColor = Color.DarkRed;

    [DataField]
    public SoundSpecifier BriefingSound = new SoundPathSpecifier("/Audio/Ambience/Antag/headrev_start.ogg");
}
