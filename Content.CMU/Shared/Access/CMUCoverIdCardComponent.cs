namespace Content.Shared.CMU14.Access;

/// <summary>
///     A spare ID card that gets its holder's name when they spawn, so it can serve as a cover identity.
///     The spawn code only names the card in the ID slot; this one is carried elsewhere.
/// </summary>
[RegisterComponent]
public sealed partial class CMUCoverIdCardComponent : Component;
