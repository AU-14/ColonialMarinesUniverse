using Content.Shared._RMC14.Marines.Skills;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Squads.Foxtrot;

/// <summary>
/// Marks the Foxtrot squad of a side. Its slots are added on top of the side's regular job slots and
/// are kept for this squad: characters that opted in fill them first, and anyone else only gets one
/// once the regular squads have no slot left for that role.
/// </summary>
[RegisterComponent]
public sealed partial class CMUFoxtrotSquadComponent : Component
{
    /// <summary>
    /// Reserved slots per job round role, e.g. SquadCombatTech.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<string, int> Slots = new();

    /// <summary>
    /// Round roles that get <see cref="Skills"/> while serving in this squad.
    /// </summary>
    [DataField]
    public HashSet<string> SkillRoles = new();

    /// <summary>
    /// Minimum skill levels given to <see cref="SkillRoles"/>; higher skills are kept.
    /// </summary>
    [DataField]
    public Dictionary<EntProtoId<SkillDefinitionComponent>, int> Skills = new();

    /// <summary>
    /// Extra vendor points per round role.
    /// </summary>
    [DataField]
    public Dictionary<string, int> ExtraVendorPoints = new();
}
