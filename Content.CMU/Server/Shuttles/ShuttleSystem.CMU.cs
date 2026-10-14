// ReSharper disable CheckNamespace

using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Marines.Skills;
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
    ///     Aircrew are not knocked down when a shuttle takes off or lands.
    /// </summary>
    private bool CMUKeepsFooting(EntityUid mob)
    {
        return _cmuSkills.HasSkill(mob, CMUAircrewSkill, 1);
    }
}
