using Content.Shared.CMU14.Threats;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Flag;

/// <summary>
///     The third parties and intel reinforcements a nation's flags rally on top of that nation's platoon, set with
///     <see cref="CMUFlagAllegianceComponent.Reinforcements"/>. A flag with such a list never rallies any other
///     third party, so a UPP flag won't buff UA reinforcements fighting alongside the UPP.
/// </summary>
[Prototype("cmuFlagReinforcements")]
public sealed partial class CMUFlagReinforcementsPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    ///     Third parties whose members are rallied, matched by the jobs their party spawns.
    /// </summary>
    [DataField]
    public List<ProtoId<ThirdPartyPrototype>> ThirdParties = new();

    /// <summary>
    ///     Job round forces (see <c>JobPrototype.RoundForce</c>) that are rallied, for forces that aren't called in
    ///     as a third party. Every job of the force is rallied, so only list forces that belong to this nation alone.
    /// </summary>
    [DataField]
    public HashSet<string> RoundForces = new();

    /// <summary>
    ///     Individual jobs that are rallied, such as colony marshals, matched exactly.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<JobPrototype>> Jobs = new();
}
