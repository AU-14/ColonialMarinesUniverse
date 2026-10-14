// ReSharper disable CheckNamespace

using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.CMU14.WallLean;
using Robust.Shared.Prototypes;

namespace Content.Server.Shuttles.Systems;

// CMU14: dropship approach times and aircrew footing.
public sealed partial class ShuttleSystem
{
    private static readonly EntProtoId<SkillDefinitionComponent> CMUAircrewSkill = "CMUSkillAircrew";

    [Dependency] private SkillsSystem _cmuSkills = default!;

    private float CMUArrivalTime(EntityUid shuttle)
    {
        return TryComp(shuttle, out DropshipComponent? dropship) && dropship.ArrivalTime is { } arrival
            ? arrival
            : DefaultArrivalTime;
    }

    /// <summary>
    ///     Aircrew, and anyone bracing against a wall, are not knocked down when a shuttle takes off or lands.
    /// </summary>
    private bool CMUKeepsFooting(EntityUid mob)
    {
        return HasComp<CMUWallLeaningComponent>(mob) || _cmuSkills.HasSkill(mob, CMUAircrewSkill, 1);
    }
}
