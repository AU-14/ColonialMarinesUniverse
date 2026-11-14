using Content.Server.Destructible;
using Content.Shared._RMC14.Power;
using Content.Shared.CMU14.DamageTracking;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Damage.Systems;
using Content.Shared.Flash;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Objectives.Components;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;

/// <summary>
/// ARCH-03 batch 2 counter directives (<see cref="EWJoeTallyConditionComponent"/>): Bear Witness (distinct
/// Judgement or Test of Faith victims, at most <see cref="EWJoeTallyConditionComponent.MaxPerCast"/> per cast,
/// RB-33). ARCH-11 moved Blind the Watch, Silence the Colony and Purge the Nest to
/// the Offer action (<c>EWJoeOfferSystem</c>, Meld or consume). This system also stamps
/// <see cref="CMULastDamagedByComponent"/> on the power entities that feed an area (APCs, reactors, portable
/// generators) for Darken a District, from their damage, before the destructible thresholds run so the killing
/// blow is stamped too. RB-38: that stamp is now Darken's only attribution (<see cref="LastJoeHit"/>).
/// </summary>
public sealed class EWJoeTallySystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private EWJoeObjectiveSystem _objectives = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeTallyConditionComponent, ObjectiveAssignedEvent>(OnAssigned);
        SubscribeLocalEvent<EWJoeTallyConditionComponent, ObjectiveAfterAssignEvent>(OnAfterAssign);
        SubscribeLocalEvent<EWJoeTallyConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);

        // origin stamps for Darken a District, before Destructible acts on the same damage
        SubscribeLocalEvent<RMCApcComponent, DamageChangedEvent>(OnDamaged<RMCApcComponent>, before: [typeof(DestructibleSystem)]);
        SubscribeLocalEvent<RMCFusionReactorComponent, DamageChangedEvent>(OnDamaged<RMCFusionReactorComponent>, before: [typeof(DestructibleSystem)]);
        SubscribeLocalEvent<RMCPortableGeneratorComponent, DamageChangedEvent>(OnDamaged<RMCPortableGeneratorComponent>, before: [typeof(DestructibleSystem)]);

        // the flash is raised on its user too; the Joe carries the ladder's own state component
        SubscribeLocalEvent<EWJoeDirectiveStateComponent, AfterFlashedEvent>(OnJoeFlashed);
    }

    #region Assignment

    private void OnAssigned(Entity<EWJoeTallyConditionComponent> ent, ref ObjectiveAssignedEvent args)
    {
        var min = Math.Max(1, ent.Comp.Min);
        var max = Math.Max(min, ent.Comp.Max);

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

        if (ent.Comp.Kind == EWJoeTallyKind.Witness)
        {
            if (ent.Comp.MinPopulation > 0
                && LivingOrganics(_zLevels.GetAllNetworkMapIds(mapId), owner, ent.Comp.MinPopulation) < ent.Comp.MinPopulation)
            {
                args.Cancelled = true;
                return;
            }

            // the flash relay lives on this
            EnsureComp<EWJoeDirectiveStateComponent>(owner);
        }

        ent.Comp.Target = _random.Next(min, max + 1);
        ent.Comp.Count = 0;
        ent.Comp.Counted.Clear();
        ent.Comp.CastCredits = 0;
    }

    private void OnAfterAssign(Entity<EWJoeTallyConditionComponent> ent, ref ObjectiveAfterAssignEvent args)
    {
        _metaData.SetEntityName(ent, Loc.GetString(ent.Comp.Title, ("count", ent.Comp.Target)), args.Meta);
        _metaData.SetEntityDescription(ent, Loc.GetString(ent.Comp.Description, ("count", ent.Comp.Target)), args.Meta);
    }

    private void OnGetProgress(Entity<EWJoeTallyConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = ent.Comp.Target <= 0 ? 1f : Math.Clamp(ent.Comp.Count / (float) ent.Comp.Target, 0f, 1f);
    }

    /// <summary>Living organic people on <paramref name="maps"/> other than <paramref name="except"/>, stopping at <paramref name="enough"/>.</summary>
    public int LivingOrganics(HashSet<MapId> maps, EntityUid except, int enough)
    {
        var count = 0;
        var query = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (count < enough && query.MoveNext(out var uid, out var mob, out var xform))
        {
            if (uid != except && maps.Contains(xform.MapID) && _mobState.IsAlive(uid, mob)
                && EWJoeTargetRules.IsOrganic(EntityManager, uid))
            {
                count++;
            }
        }

        return count;
    }

    #endregion

    #region Origin stamps

    private void OnDamaged<T>(Entity<T> ent, ref DamageChangedEvent args) where T : IComponent
    {
        Stamp(ent, args);
    }

    /// <summary>Remembers who last hurt <paramref name="uid"/>, resolved back to the Joe where it was the Joe.</summary>
    private void Stamp(EntityUid uid, DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.Origin is not { } origin)
            return;

        var stamp = EnsureComp<CMULastDamagedByComponent>(uid);
        stamp.Origin = _objectives.ResolveKiller(origin) ?? origin;
        stamp.At = _timing.CurTime;
    }

    /// <summary>The Joe that hit <paramref name="uid"/> last, if that was within <paramref name="window"/>.</summary>
    public EntityUid? LastJoeHit(EntityUid uid, TimeSpan window)
    {
        if (!TryComp(uid, out CMULastDamagedByComponent? stamp)
            || stamp.Origin is not { } origin
            || _timing.CurTime - stamp.At > window
            || !HasComp<EWJoeComponent>(origin))
        {
            return null;
        }

        return origin;
    }

    #endregion

    #region Credit

    private void OnJoeFlashed(Entity<EWJoeDirectiveStateComponent> ent, ref AfterFlashedEvent args)
    {
        // the Joe's own Test of Faith and Judgement flash with no device; anything else is not its witness
        if (args.User != ent.Owner || args.Used != null || args.Target == ent.Owner)
            return;

        if (!TryComp(ent, out EWJoeComponent? comp))
            return;

        Credit((ent, comp), EWJoeTallyKind.Witness, args.Target, 1);
    }

    /// <summary>
    /// Adds <paramref name="weight"/> to every unfinished directive of <paramref name="kind"/> that has not counted
    /// <paramref name="target"/>. RB-33: a directive with <see cref="EWJoeTallyConditionComponent.MaxPerCast"/>
    /// counts at most that many targets per cast. A cast (Test of Faith, a Judgement cone) flashes everyone it
    /// reaches inside one action handler, so every credit of one cast lands in the same server tick; a target
    /// past the cap is not marked counted and can still count on a later cast.
    /// </summary>
    public void Credit(Entity<EWJoeComponent> joe, EWJoeTallyKind kind, EntityUid target, int weight)
    {
        var tick = _timing.CurTick;
        foreach (var objective in joe.Comp.CurrentObjectives)
        {
            if (joe.Comp.CompletedCredit.ContainsKey(objective)
                || !TryComp(objective, out EWJoeTallyConditionComponent? tally)
                || tally.Kind != kind
                || tally.Count >= tally.Target
                || tally.Counted.Contains(target))
            {
                continue;
            }

            if (tally.MaxPerCast > 0)
            {
                if (tally.CastTick != tick)
                {
                    tally.CastTick = tick;
                    tally.CastCredits = 0;
                }

                if (tally.CastCredits >= tally.MaxPerCast)
                    continue;

                tally.CastCredits++;
            }

            tally.Counted.Add(target);
            tally.Count += weight;
        }
    }

    #endregion
}
