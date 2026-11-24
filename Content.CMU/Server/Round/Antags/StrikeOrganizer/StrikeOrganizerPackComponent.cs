using Content.Shared.CMU14.Round.Antags.StrikeOrganizer;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Round.Antags.StrikeOrganizer;

/// <summary>
/// The organizer's radio pack: prints petitions and transmits one-way communiques to the
/// CMB. Whoever holds the pack holds the strike's printing press; the venture ledger
/// itself stays on the rule, so losing the pack never erases the strike.
/// </summary>
[RegisterComponent]
public sealed partial class StrikeOrganizerPackComponent : Component
{
    [DataField]
    public EntProtoId RedraftActionId = "CMUActionStrikeRedraft";

    [DataField]
    public EntProtoId TransmitActionId = "CMUActionStrikeFax";

    public EntityUid? RedraftAction;
    public EntityUid? TransmitAction;
}
