using Content.Shared.Roles;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.NPC.Prototypes;
using Content.Shared._RMC14.Weapons.Ranged.IFF;

namespace Content.Shared.AU14.CLF;

/// <summary>
/// When held and used on a humanoid, opens a prompt asking if they want to join the CLF.
/// Requires ink cartridges loaded in the gun's storage.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TattooGunComponent : Component
{
    /// <summary>
    /// Faction to add to the tattooed target.
    /// </summary>
    [DataField]
    public ProtoId<NpcFactionPrototype> Faction = "CLF";

    /// <summary>
    /// Duration of the tattooing DoAfter in seconds.
    /// </summary>
    [DataField]
    public float DoAfterDuration = 10f;

    /// <summary>
    /// Briefing message shown to the recruit.
    /// </summary>
    [DataField]
    public LocId Briefing = "clf-tattoo-recruit-briefing";

    /// <summary>
    /// Mind role prototype to add.
    /// </summary>
    [DataField]
    public string Role = "MindRoleCLFRecruit";

    [DataField]
    public EntProtoId<IFFFactionComponent> IFF = "FactionCLF";

    /// <summary>
    /// Sound played when the recruit receives their briefing.
    /// </summary>
    [DataField]
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/Ambience/Antag/headrev_start.ogg");

    /// <summary>
    /// Department IDs whose members cannot be tattooed.
    /// </summary>
    [DataField]
    public List<ProtoId<DepartmentPrototype>> BlockedDepartments = new()
    {
        "AU14DepartmentColonyCommand",
        "AU14DepartmentGovernmentForces",
    };
}


