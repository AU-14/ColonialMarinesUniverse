using Content.Shared._RMC14.Areas;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;

/// <summary>
/// Pins, titles and scores <see cref="EWJoeAssimilateConditionComponent"/> objectives.
/// </summary>
public sealed partial class EWJoeAssimilateConditionSystem : EntitySystem
{
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private AreaSystem _area = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeAssimilateConditionComponent, ObjectiveAssignedEvent>(OnAssigned);
        SubscribeLocalEvent<EWJoeAssimilateConditionComponent, ObjectiveAfterAssignEvent>(OnAfterAssign);
        SubscribeLocalEvent<EWJoeAssimilateConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
        SubscribeLocalEvent<EWJoeAssimilateConditionComponent, EWJoeObjectiveStillPossibleEvent>(OnStillPossible);
    }

    /// <summary>ARCH-02: a destroyed console, or one someone else assimilated, can never be finished.</summary>
    private void OnStillPossible(Entity<EWJoeAssimilateConditionComponent> ent, ref EWJoeObjectiveStillPossibleEvent args)
    {
        if (!CanStillFinish(ent, args.Owner))
            args.Possible = false;
    }

    private void OnAssigned(Entity<EWJoeAssimilateConditionComponent> ent, ref ObjectiveAssignedEvent args)
    {
        if (ent.Comp.Target != null)
            return;

        if (args.Mind.OwnedEntity is not { } owner)
        {
            args.Cancelled = true;
            return;
        }

        var mapId = Transform(owner).MapID;
        if (mapId == MapId.Nullspace)
        {
            args.Cancelled = true;
            return;
        }

        // the planet is a z-level network of maps, not one map
        var maps = _zLevels.GetAllNetworkMapIds(mapId);
        var candidates = FindCandidates(maps, ent.Comp.Whitelist, args.Mind);
        if (candidates.Count == 0)
        {
            args.Cancelled = true;
            return;
        }

        ent.Comp.Target = _random.Pick(candidates);
    }

    private void OnAfterAssign(Entity<EWJoeAssimilateConditionComponent> ent, ref ObjectiveAfterAssignEvent args)
    {
        if (ent.Comp.Target is not { } target || !Exists(target))
            return;

        var consoleName = Name(target);
        var areaName = Loc.GetString("cmu-ew-joe-objective-area-unknown");
        if (_area.TryGetArea(Transform(target).Coordinates, out var area, out _))
            areaName = Name(area.Value);

        _metaData.SetEntityName(ent, Loc.GetString(ent.Comp.Title, ("console", consoleName), ("area", areaName)), args.Meta);
        _metaData.SetEntityDescription(ent, Loc.GetString(ent.Comp.Description, ("console", consoleName), ("area", areaName)), args.Meta);
    }

    private void OnGetProgress(Entity<EWJoeAssimilateConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = ent.Comp.Target is { } target
            && Exists(target)
            && TryComp(target, out EWJoeAssimilatedConsoleComponent? assimilated)
            // an unattributed assimilation counts; one attributed to someone else does not
            && (assimilated.AssimilatedBy == null || assimilated.AssimilatedBy == args.Mind.OwnedEntity)
            ? 1f
            : 0f;
    }

    /// <summary>
    /// True while the objective can still be finished by <paramref name="owner"/>: the pinned console
    /// exists and is not already assimilated by someone else. A destroyed console or a foreign
    /// assimilation leaves the objective unfinishable, and the ladder withdraws it.
    /// </summary>
    public bool CanStillFinish(Entity<EWJoeAssimilateConditionComponent> ent, EntityUid? owner)
    {
        if (ent.Comp.Target is not { } target || !Exists(target))
            return false;

        return !TryComp(target, out EWJoeAssimilatedConsoleComponent? assimilated)
            || assimilated.AssimilatedBy == null
            || assimilated.AssimilatedBy == owner;
    }

    /// <summary>
    /// Every whitelisted console on <paramref name="maps"/> that is not assimilated yet and not already
    /// the target of another assimilate objective on <paramref name="mind"/>.
    /// </summary>
    public List<EntityUid> FindCandidates(HashSet<MapId> maps, EntityWhitelist whitelist, MindComponent mind)
    {
        var taken = new HashSet<EntityUid>();
        foreach (var objective in mind.Objectives)
        {
            if (TryComp(objective, out EWJoeAssimilateConditionComponent? other) && other.Target is { } otherTarget)
                taken.Add(otherTarget);
        }

        var candidates = new List<EntityUid>();
        if (whitelist.Components == null)
            return candidates;

        foreach (var name in whitelist.Components)
        {
            if (!_factory.TryGetRegistration(name, out var registration))
            {
                Log.Error($"Assimilate objective whitelist names unknown component {name}");
                continue;
            }

            foreach (var (uid, _) in EntityManager.GetAllComponents(registration.Type))
            {
                if (candidates.Contains(uid) || taken.Contains(uid))
                    continue;

                if (!maps.Contains(Transform(uid).MapID) || HasComp<EWJoeAssimilatedConsoleComponent>(uid))
                    continue;

                candidates.Add(uid);
            }
        }

        return candidates;
    }
}
