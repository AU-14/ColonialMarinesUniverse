namespace Content.Server.CMU14.Round.Antags.StrikeOrganizer;

/// <summary>
/// A colonist who signed a strike petition and joined the venture as an antagonist.
/// Participation marker only: the venture ledger on the rule owns the roster and
/// succession, so this component carries no back-references.
/// </summary>
[RegisterComponent]
public sealed partial class StrikeParticipantComponent : Component;
