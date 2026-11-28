using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Squads;

/// <summary>
///     Puts a spawning player into <see cref="Squad"/>, creating the squad if nobody has yet.
///     Unlike AssignSquad, which only joins squads that already exist, this works for the first
///     member of a squad nothing else creates at round start (the corporate Overwatch tabs).
/// </summary>
[RegisterComponent]
[Access(typeof(CMUAssignSquadSystem))]
public sealed partial class CMUAssignSquadComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Squad;
}
