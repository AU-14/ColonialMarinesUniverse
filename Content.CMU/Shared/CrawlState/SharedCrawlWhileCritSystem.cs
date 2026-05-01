using Content.Shared.ActionBlocker;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared._RMC14.Xenonids.Construction.Nest;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.CrawlState;

public sealed class SharedCrawlWhileCritSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CrawlWhileCritComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<CrawlWhileCritComponent, BeforeDamageChangedEvent>(OnBeforeDamage);

        SubscribeLocalEvent<ActiveCrawlWhileCritComponent, UpdateCanMoveEvent>(OnUpdateCanMove,
            after: new[] { typeof(MobStateSystem) });
        SubscribeLocalEvent<ActiveCrawlWhileCritComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    private void OnMobStateChanged(Entity<CrawlWhileCritComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Critical)
        {
            if (IsWithinCrawlWindow(ent))
                TryBeginCrawl(ent);
            else
                ShowServerPopup(ent, "cmu-crawl-crit-past-window", PopupType.SmallCaution);
            return;
        }

        StopCrawl(ent.Owner);
    }

    private void OnBeforeDamage(Entity<CrawlWhileCritComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TryComp<ActiveCrawlWhileCritComponent>(ent, out var active) || !active.Started)
            return;

        if (!IsAbortingDamage(ent.Comp, args.Damage))
            return;

        ShowServerPopup(ent, "cmu-crawl-crit-stopped-hit", PopupType.SmallCaution);
        StopCrawl(ent.Owner);
    }

    private bool IsAbortingDamage(CrawlWhileCritComponent comp, DamageSpecifier damage)
    {
        foreach (var groupId in comp.AbortOnDamageGroups)
        {
            if (!_protoManager.TryIndex(groupId, out var group))
                continue;

            foreach (var type in group.DamageTypes)
            {
                if (damage.DamageDict.TryGetValue(type, out var amount) && amount > FixedPoint2.Zero)
                    return true;
            }
        }

        return false;
    }

    private bool IsWithinCrawlWindow(Entity<CrawlWhileCritComponent> ent)
    {
        if (!TryComp<DamageableComponent>(ent, out var damageable))
            return false;

        if (!_thresholds.TryGetThresholdForState(ent, MobState.Critical, out var critThreshold))
            return false;

        var overflow = damageable.TotalDamage - critThreshold.Value;
        return overflow <= ent.Comp.CrawlWindow;
    }

    private void TryBeginCrawl(Entity<CrawlWhileCritComponent> ent)
    {
        if (HasComp<ActiveCrawlWhileCritComponent>(ent))
            return;

        var active = EnsureComp<ActiveCrawlWhileCritComponent>(ent);
        active.StartAt = _timing.CurTime + ent.Comp.ActivationDelay;
        active.Started = false;
        Dirty(ent.Owner, active);
    }

    private void StopCrawl(EntityUid ent)
    {
        if (!HasComp<ActiveCrawlWhileCritComponent>(ent))
            return;

        // Refresh AFTER removal so our ActiveCrawlWhileCritComponent handlers don't keep the mob crawling.
        RemComp<ActiveCrawlWhileCritComponent>(ent);
        _blocker.UpdateCanMove(ent);
        _speed.RefreshMovementSpeedModifiers(ent);
    }

    private void OnUpdateCanMove(Entity<ActiveCrawlWhileCritComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (!ent.Comp.Started)
            return;

        if (!TryComp<MobStateComponent>(ent, out var state) || state.CurrentState != MobState.Critical)
            return;

        if (TryComp<BuckleComponent>(ent, out var buckle) && buckle.Buckled)
            return;

        if (HasComp<XenoNestedComponent>(ent))
            return;

        args.Uncancel();
    }

    private void OnRefreshSpeed(Entity<ActiveCrawlWhileCritComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!ent.Comp.Started)
            return;

        if (!TryComp<CrawlWhileCritComponent>(ent, out var crawl))
            return;

        args.ModifySpeed(crawl.WalkSpeedModifier, crawl.SprintSpeedModifier);
    }

    private void ShowServerPopup(EntityUid target, string locKey, PopupType type)
    {
        if (_net.IsClient)
            return;

        _popup.PopupEntity(Loc.GetString(locKey), target, target, type);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ActiveCrawlWhileCritComponent, CrawlWhileCritComponent>();
        while (query.MoveNext(out var uid, out var active, out var crawl))
        {
            if (!IsWithinCrawlWindow((uid, crawl)))
            {
                StopCrawl(uid);
                continue;
            }

            if (active.Started)
                continue;

            if (now < active.StartAt)
                continue;

            active.Started = true;
            Dirty(uid, active);

            _blocker.UpdateCanMove(uid);
            _speed.RefreshMovementSpeedModifiers(uid);

            ShowServerPopup(uid, GetActivationPopupKey(uid), PopupType.Small);
        }
    }

    private string GetActivationPopupKey(EntityUid ent)
    {
        if (HasComp<XenoNestedComponent>(ent))
            return "cmu-crawl-crit-nested";

        if (TryComp<BuckleComponent>(ent, out var buckle) && buckle.Buckled)
            return "cmu-crawl-crit-restrained";

        if (_container.IsEntityInContainer(ent))
            return "cmu-crawl-crit-in-container";

        return "cmu-crawl-crit-started";
    }
}
