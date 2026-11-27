using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.CMU14.Allegiance;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Flag;

/// <summary>
///     Ties a flag to the factions it represents. It only ever rallies the sides fielding one of those allegiances,
///     so a captured flag never helps the side that took it. A flag without allegiances rallies whoever bears it.
/// </summary>
[RegisterComponent]
[Access(typeof(CMUFlagAuraSystem))]
public sealed partial class CMUFlagAllegianceComponent : Component
{
    [DataField]
    public HashSet<ProtoId<AllegiancePrototype>> Allegiances = new();

    /// <summary>
    ///     The third parties and reinforcements of this flag's nation. When set, the flag rallies them as well as its
    ///     side, and never rallies any other third party. With no <see cref="Allegiances"/>, it rallies only them.
    /// </summary>
    [DataField]
    public ProtoId<CMUFlagReinforcementsPrototype>? Reinforcements;
}

/// <summary>
///     Asks which IFF factions field any of <see cref="Allegiances"/> this round, e.g. GOVFOR when the government
///     platoon is UA. Set <see cref="Handled"/> when the round's sides are known; otherwise flags fall back to
///     rallying their bearer's side.
/// </summary>
[ByRefEvent]
public record struct CMUGetFlagOwnersEvent(
    IReadOnlySet<ProtoId<AllegiancePrototype>> Allegiances,
    HashSet<EntProtoId<IFFFactionComponent>> Owners,
    bool Handled = false);
