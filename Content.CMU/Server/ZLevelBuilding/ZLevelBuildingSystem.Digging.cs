using Content.Server.GameTicking;
using Content.Shared.CCVar;
using Content.Shared.CMU14.ZLevelBuilding;
using Content.Shared.CMU14.ZLevels;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Robust.Shared.Configuration;

namespace Content.Server.CMU14.ZLevelBuilding;

public sealed partial class ZLevelBuildingSystem
{
    [Dependency] private IConfigurationManager _diggingConfig = default!;

    [Dependency] private GameTicker _diggingTicker = default!;

    /// <summary>Controls new downward excavations, without blocking upward digging or existing shafts.</summary>
    public bool CanDigDown => _diggingConfig.GetCVar(CCVars.AllowDiggingDown)
        && _diggingTicker.CurrentPreset?.AllowDiggingDown != false;

    /// <summary>
    /// Whether going down from <paramref name="mapUid"/> at <paramref name="worldPos"/> would be a NEW excavation:
    /// false into a stone level that already exists below (an existing shaft), or onto an authored walkable floor
    /// below, which creates no stone. Shared by the stair condition and <see cref="PrepareStoneForStair"/>, so the
    /// two can never disagree.
    /// </summary>
    private bool IsNewDownExcavation(EntityUid mapUid, System.Numerics.Vector2 worldPos)
    {
        if (!TryComp<CMUZLevelMapComponent>(mapUid, out var level) || level.MapBelow is not { } below)
            return true;

        // Existing stone (localized or not) is an existing shaft. Without stone, only an authored floor is no excavation.
        return !HasComp<ZGeneratedStoneComponent>(below) && !HasWalkableFloorAt(below, worldPos);
    }

    /// <summary>
    /// Answers <see cref="ZDigDownAllowed"/>: a down-stair is allowed while digging down is, or where it is no new
    /// excavation (see <see cref="IsNewDownExcavation"/>).
    /// </summary>
    private void OnDigDownQuery(ref ZDigDownQueryEvent args)
    {
        if (!IsNewDownExcavation(args.Map, args.WorldPos))
            return;

        // CMU14: the depth cap holds even where digging down is allowed.
        var below = (TryComp<CMUZLevelMapComponent>(args.Map, out var level) ? level.Depth : 0) - 1;
        if (CanDigDown && IsDepthWithinBuildCap(below))
            return;

        args.Allowed = false;
    }

    /// <summary>
    /// CMU14: whether a NEW z-level may be created at <paramref name="depth"/>, within
    /// <see cref="CMUZLevelsCVars.BuildMinDepth"/>..<see cref="CMUZLevelsCVars.BuildMaxDepth"/>. The sanity cap that
    /// stops endless building up or digging down; checked wherever a runtime level is created
    /// (<see cref="EnsureNeighborLevel"/>, <see cref="CreateStoneBelow"/>). Existing levels are never affected.
    /// </summary>
    public bool IsDepthWithinBuildCap(int depth)
    {
        return depth <= _diggingConfig.GetCVar(CMUZLevelsCVars.BuildMaxDepth)
            && depth >= _diggingConfig.GetCVar(CMUZLevelsCVars.BuildMinDepth);
    }
}
