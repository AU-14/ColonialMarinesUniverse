using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Power;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;

/// <summary>
/// ARCH-03 N7 "Darken a District" (Objectives report §6a): pins a powered area, runs the
/// Armed → Dark → Complete state machine at 1 Hz, attributes the blackout to the Joe (RB-38: only its own recent
/// hit on an APC, reactor or generator feeding the area, read from <c>CMULastDamagedByComponent</c>; standing in
/// the district is not enough),
/// and re-pins or gives up on a blackout the Joe did not cause. RMC raises no power-changed event (only the
/// protected <c>PowerUpdated</c> override), so the lighting channel is polled.
/// </summary>
public sealed class EWJoeDarkenSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EWJoeMessageSystem _messages = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedRMCPowerSystem _power = default!;
    [Dependency] private EWJoeTallySystem _tally = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    private static readonly TimeSpan PollEvery = TimeSpan.FromSeconds(1);

    private TimeSpan _nextPoll;

    /// <summary>
    /// Tests only: stands in for the area's lighting channel, since driving RMC area power in a test means a
    /// whole reactor and APC network. Null in play.
    /// </summary>
    public Func<EntityUid, bool>? PoweredOverride;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeDarkenConditionComponent, ObjectiveAssignedEvent>(OnAssigned);
        SubscribeLocalEvent<EWJoeDarkenConditionComponent, ObjectiveAfterAssignEvent>(OnAfterAssign);
        SubscribeLocalEvent<EWJoeDarkenConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
        SubscribeLocalEvent<EWJoeDarkenConditionComponent, EWJoeObjectiveStillPossibleEvent>(OnStillPossible);
    }

    #region Objective

    private void OnAssigned(Entity<EWJoeDarkenConditionComponent> ent, ref ObjectiveAssignedEvent args)
    {
        if (ent.Comp.Area != null)
            return;

        if (args.Mind.OwnedEntity is not { } owner || Transform(owner).MapID == MapId.Nullspace)
        {
            args.Cancelled = true;
            return;
        }

        // only an area that is lit right now can be darkened by the Joe; a dark planet refuses the directive
        var candidates = FindPoweredAreas(_zLevels.GetAllNetworkMapIds(Transform(owner).MapID), PinnedAreas(args.Mind));
        if (candidates.Count == 0)
        {
            args.Cancelled = true;
            return;
        }

        var (area, name) = _random.Pick(candidates);
        ent.Comp.Area = area;
        ent.Comp.AreaName = name;
        ent.Comp.Owner = owner;
        ent.Comp.MindId = args.MindId;
        ent.Comp.State = EWJoeDarkenState.Armed;
        ent.Comp.Exhausted = false;
    }

    private void OnAfterAssign(Entity<EWJoeDarkenConditionComponent> ent, ref ObjectiveAfterAssignEvent args)
    {
        Rename(ent, args.Meta);
    }

    private void Rename(Entity<EWJoeDarkenConditionComponent> ent, MetaDataComponent? meta = null)
    {
        var minutes = Math.Max(1, (int) Math.Round(ent.Comp.DarkDuration.TotalMinutes));
        _metaData.SetEntityName(ent, Loc.GetString(ent.Comp.Title, ("area", ent.Comp.AreaName)), meta);
        _metaData.SetEntityDescription(ent, Loc.GetString(ent.Comp.Description, ("area", ent.Comp.AreaName), ("minutes", minutes)), meta);
    }

    private void OnGetProgress(Entity<EWJoeDarkenConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = ent.Comp.State switch
        {
            EWJoeDarkenState.Complete => 1f,
            // never reads complete before the state says so
            EWJoeDarkenState.Dark when ent.Comp.DarkDuration > TimeSpan.Zero =>
                Math.Clamp((float) ((_timing.CurTime - ent.Comp.DarkSince) / ent.Comp.DarkDuration), 0f, 0.99f),
            _ => 0f,
        };
    }

    /// <summary>ARCH-02: with no powered area left to re-pin to, the directive cannot finish.</summary>
    private void OnStillPossible(Entity<EWJoeDarkenConditionComponent> ent, ref EWJoeObjectiveStillPossibleEvent args)
    {
        if (ent.Comp.Exhausted && ent.Comp.State != EWJoeDarkenState.Complete)
            args.Possible = false;
    }

    #endregion

    #region State machine

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextPoll)
            return;

        _nextPoll = _timing.CurTime + PollEvery;

        var query = EntityQueryEnumerator<EWJoeDarkenConditionComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.State == EWJoeDarkenState.Complete || comp.Exhausted)
                continue;

            Poll((uid, comp));
        }
    }

    /// <summary>One step of the state machine. Public so tests can step it without waiting a second.</summary>
    public void Poll(Entity<EWJoeDarkenConditionComponent> ent)
    {
        var now = _timing.CurTime;
        // follow the mind across a respawn into a new body
        if (TryComp(ent.Comp.MindId, out MindComponent? mind) && mind.OwnedEntity is { } owned)
            ent.Comp.Owner = owned;

        if (ent.Comp.Owner is not { } joe || TerminatingOrDeleted(joe))
            return;

        if (ent.Comp.Area is not { } area || TerminatingOrDeleted(area))
        {
            RePin(ent, joe);
            return;
        }

        var powered = IsPowered(area);
        switch (ent.Comp.State)
        {
            case EWJoeDarkenState.Armed:
                if (powered)
                    return;

                if (!CausedByJoe(ent.Comp, joe, area))
                {
                    // the district fell dark on its own (a generator ran dry, an OB, someone else)
                    _messages.Notify(joe, Loc.GetString("cmu-ew-joe-darken-not-yours", ("area", ent.Comp.AreaName)));
                    RePin(ent, joe);
                    return;
                }

                ent.Comp.State = EWJoeDarkenState.Dark;
                ent.Comp.DarkSince = now;
                _messages.Notify(joe, Loc.GetString("cmu-ew-joe-darken-dark", ("area", ent.Comp.AreaName)));
                return;

            case EWJoeDarkenState.Dark:
                if (powered)
                {
                    // the light came back before the time was up: start over
                    ent.Comp.State = EWJoeDarkenState.Armed;
                    _messages.Notify(joe, Loc.GetString("cmu-ew-joe-darken-relit", ("area", ent.Comp.AreaName)));
                    return;
                }

                if (now - ent.Comp.DarkSince >= ent.Comp.DarkDuration)
                    ent.Comp.State = EWJoeDarkenState.Complete;

                return;
        }
    }

    /// <summary>
    /// The Joe caused the blackout: within the window it hit an APC of the area, or a reactor or portable
    /// generator on the area's power network (area power is fed per z-network; there are no cables on area-power
    /// maps). RB-38: being in the district when the lights went out no longer counts.
    /// </summary>
    public bool CausedByJoe(EWJoeDarkenConditionComponent comp, EntityUid joe, EntityUid area)
    {
        var maps = new HashSet<MapId>();
        var apcs = EntityQueryEnumerator<RMCApcComponent, TransformComponent>();
        while (apcs.MoveNext(out var uid, out var apc, out var xform))
        {
            if (apc.Area != area)
                continue;

            if (_tally.LastJoeHit(uid, comp.AttributionWindow) == joe)
                return true;

            maps.UnionWith(_zLevels.GetAllNetworkMapIds(xform.MapID));
        }

        var reactors = EntityQueryEnumerator<RMCFusionReactorComponent, TransformComponent>();
        while (reactors.MoveNext(out var uid, out _, out var xform))
        {
            if (maps.Contains(xform.MapID) && _tally.LastJoeHit(uid, comp.AttributionWindow) == joe)
                return true;
        }

        var generators = EntityQueryEnumerator<RMCPortableGeneratorComponent, TransformComponent>();
        while (generators.MoveNext(out var uid, out _, out var xform))
        {
            if (maps.Contains(xform.MapID) && _tally.LastJoeHit(uid, comp.AttributionWindow) == joe)
                return true;
        }

        return false;
    }

    /// <summary>Moves the pin to another powered area on the Joe's planet, or marks the directive exhausted.</summary>
    private void RePin(Entity<EWJoeDarkenConditionComponent> ent, EntityUid joe)
    {
        var exclude = new HashSet<EntityUid>();
        if (_mind.TryGetMind(joe, out _, out var mind))
            exclude = PinnedAreas(mind);

        if (ent.Comp.Area is { } current)
            exclude.Add(current);

        var mapId = Transform(joe).MapID;
        var candidates = mapId == MapId.Nullspace
            ? new List<(EntityUid, string)>()
            : FindPoweredAreas(_zLevels.GetAllNetworkMapIds(mapId), exclude);

        if (candidates.Count == 0)
        {
            ent.Comp.Exhausted = true;
            _messages.Notify(joe, Loc.GetString("cmu-ew-joe-darken-exhausted"));
            return;
        }

        var (area, name) = _random.Pick(candidates);
        ent.Comp.Area = area;
        ent.Comp.AreaName = name;
        ent.Comp.State = EWJoeDarkenState.Armed;
        Rename(ent);
        _messages.Notify(joe, Loc.GetString("cmu-ew-joe-darken-repinned", ("area", name)));
    }

    #endregion

    #region Areas

    /// <summary>
    /// Every area on <paramref name="maps"/> that can be darkened right now: it has an APC, it is not
    /// always-powered, and its lighting is on. Areas in <paramref name="exclude"/> are skipped.
    /// </summary>
    public List<(EntityUid Area, string Name)> FindPoweredAreas(HashSet<MapId> maps, HashSet<EntityUid> exclude)
    {
        var seen = new HashSet<EntityUid>();
        var found = new List<(EntityUid, string)>();
        var apcs = EntityQueryEnumerator<RMCApcComponent, TransformComponent>();
        while (apcs.MoveNext(out _, out var apc, out var xform))
        {
            if (apc.Area is not { } area || !maps.Contains(xform.MapID) || exclude.Contains(area) || !seen.Add(area))
                continue;

            if (TerminatingOrDeleted(area))
                continue;

            if (TryComp(area, out AreaComponent? areaComp) && areaComp.AlwaysPowered)
                continue;

            if (!IsPowered(area))
                continue;

            found.Add((area, Name(area)));
        }

        return found;
    }

    public bool IsPowered(EntityUid area)
    {
        return PoweredOverride?.Invoke(area) ?? _power.IsAreaPowered(area, RMCPowerChannel.Lighting);
    }

    /// <summary>Areas already pinned by the mind's other Darken directives.</summary>
    private HashSet<EntityUid> PinnedAreas(MindComponent mind)
    {
        var pinned = new HashSet<EntityUid>();
        foreach (var objective in mind.Objectives)
        {
            if (TryComp(objective, out EWJoeDarkenConditionComponent? other) && other.Area is { } area)
                pinned.Add(area);
        }

        return pinned;
    }

    #endregion
}
