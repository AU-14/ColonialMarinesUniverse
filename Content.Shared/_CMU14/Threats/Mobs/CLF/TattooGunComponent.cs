using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Roles;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Threats.Mobs.CLF;

/// <summary>
///     When held and used on a humanoid, opens a prompt asking if they want to join the CLF.
///     Requires ink cartridges loaded in the gun's storage.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TattooGunComponent : Component
{
    /// <summary>
    ///     Department IDs whose members cannot be tattooed.
    /// </summary>
    [DataField]
    public List<ProtoId<DepartmentPrototype>> BlockedDepartments = new()
    {
        "AU14DepartmentColonyCommand",
        "AU14DepartmentGovernmentForces"
    };

    /// <summary>
    ///     Briefing message shown to the recruit.
    /// </summary>
    [DataField]
    public LocId Briefing = "clf-tattoo-recruit-briefing";

    /// <summary>
    ///     Duration of the tattooing DoAfter in seconds.
    /// </summary>
    [DataField]
    public float DoAfterDuration = 10f;

    /// <summary>
    ///     Faction to add to the tattooed target.
    /// </summary>
    [DataField]
    public ProtoId<NpcFactionPrototype> Faction = "CLF";

    [DataField]
    public EntProtoId<IFFFactionComponent> IFF = "FactionCLF";

    /// <summary>
    ///     Mind role prototype to add.
    /// </summary>
    [DataField]
    public string Role = "MindRoleCLFRecruit";

    /// <summary>
    ///     Sound played when the recruit receives their briefing.
    /// </summary>
    [DataField]
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/Ambience/Antag/headrev_start.ogg");
}
