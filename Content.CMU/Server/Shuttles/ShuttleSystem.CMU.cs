// ReSharper disable CheckNamespace

using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Synth;
using Content.Shared.CMU14.WallLean;
using Robust.Shared.Prototypes;

namespace Content.Server.Shuttles.Systems;

// CMU14: aircrew footing.
public sealed partial class ShuttleSystem
{
    private static readonly EntProtoId<SkillDefinitionComponent> CMUAircrewSkill = "CMUSkillAircrew";

    [Dependency] private SkillsSystem _cmuSkills = default!;

    /// <summary>
    ///     Aircrew (synthetics count as aircrew), and anyone bracing against a wall, are not knocked down when a
    ///     shuttle takes off or lands.
    /// </summary>
    private bool CMUKeepsFooting(EntityUid mob)
    {
        return HasComp<CMUWallLeaningComponent>(mob) ||
               HasComp<SynthComponent>(mob) ||
               _cmuSkills.HasSkill(mob, CMUAircrewSkill, 1);
    }
}
