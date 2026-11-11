using Content.Shared._RMC14.Language.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Ops.ThirdParty;

/// <summary>
/// Who called a third party in from a console. Passed to <see cref="ThirdPartySystem.SpawnThirdParty"/>.
/// </summary>
/// <param name="Name">The character who made the call.</param>
/// <param name="Faction">The faction they called on behalf of, e.g. "UPP" or "Colony Administration".</param>
/// <param name="Languages">That faction's languages, for parties that speak their caller's language.</param>
/// <param name="Actor">The caller's entity, for the admin log.</param>
/// <param name="Console">The console they called from, for the admin log.</param>
public readonly record struct ThirdPartyCaller(
    string Name,
    string Faction,
    IReadOnlyList<ProtoId<LanguagePrototype>>? Languages = null,
    EntityUid? Actor = null,
    EntityUid? Console = null);

/// <summary>
/// Put on each member of a called-in third party. The ghost role's description names the caller, and whoever
/// takes the body is told who called them in.
/// </summary>
[RegisterComponent]
public sealed partial class CMUThirdPartyCallerComponent : Component
{
    [DataField]
    public string CallerName = string.Empty;

    [DataField]
    public string CallerFaction = string.Empty;

    /// <summary>
    /// Whether the player in this body has already been told who called them in.
    /// </summary>
    [DataField]
    public bool Notified;
}
