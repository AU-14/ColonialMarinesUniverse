using Content.Server.CMU14.TacticalMap;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Body;
using Content.Shared._RMC14.Marines;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// CMU14 ARCH-606: the Tracker caste. Its armour counts for less, and it marks prey for the clan: the honourable
/// prey mark (YautjaMarkSystem, unchanged), and the prey on every Yautja bracer map for MarkDuration. A prey mark
/// alone shows on no map, so the clan's map is told here through the trap telemetry (YautjaPreyTrackingSystem).
/// The shorter cloak cooldown is read by YautjaCloakSystem.
/// </summary>
public sealed class BadBloodTrackerSystem : EntitySystem
{
    [Dependency] private YautjaMarkSystem _mark = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private YautjaPowerSystem _power = default!;
    [Dependency] private YautjaPreyTrackingSystem _preyTracking = default!;
    [Dependency] private SharedRMCBloodstreamSystem _bloodstream = default!;
    [Dependency] private BadBloodSystem _badBlood = default!;
    [Dependency] private BadBloodCampRuleSystem _rule = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private Content.Shared._RMC14.Areas.AreaSystem _area = default!;

    private readonly List<EntityUid> _marines = new();

    /// <summary>
    /// How many early warnings reached a Tracker. Instrumentation for the tests.
    /// </summary>
    public int EarlyWarningsSent { get; private set; }

    public override void Initialize()
    {
        SubscribeLocalEvent<BadBloodTrackerComponent, CMGetArmorEvent>(OnGetArmor);
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<BadBloodTrackerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var tracker, out var xform))
        {
            if (now < tracker.NextBloodTrail || !_mobState.IsAlive(uid))
                continue;

            tracker.NextBloodTrail = now + tracker.BloodTrailEvery;
            var here = _transform.GetMapCoordinates(uid, xform);
            UpdateBloodTrail((uid, tracker), here);
            UpdateEarlyWarning((uid, tracker), here, now);
        }
    }

    /// <summary>
    /// CMU14 ECS-704: every bleeding marine within BloodTrailRange of the Tracker shows on the clan's bracer maps for
    /// BloodTrailShowFor. Returns how many were shown.
    /// </summary>
    public int UpdateBloodTrail(Entity<BadBloodTrackerComponent> tracker, Robust.Shared.Map.MapCoordinates here)
    {
        var shown = 0;
        var range = tracker.Comp.BloodTrailRange * tracker.Comp.BloodTrailRange;
        var marines = EntityQueryEnumerator<MarineComponent, TransformComponent>();
        while (marines.MoveNext(out var marine, out _, out var xform))
        {
            if (xform.MapID != here.MapId ||
                !_mobState.IsAlive(marine) ||
                _badBlood.IsBadBlood(marine) ||
                (_transform.GetWorldPosition(xform) - here.Position).LengthSquared() > range ||
                !_bloodstream.IsBleeding(marine))
            {
                continue;
            }

            _preyTracking.Track(marine, tracker.Comp.BloodTrailShowFor);
            shown++;
        }

        return shown;
    }

    /// <summary>
    /// CMU14 ECS-704: a marine closing on one of the clan's Totems or ordered Hunt Grounds, within its contest radius plus WarningPadding,
    /// warns the Tracker before any contest starts, once per Totem per WarningCooldown, and shows on the clan's map.
    /// </summary>
    public void UpdateEarlyWarning(Entity<BadBloodTrackerComponent> tracker, Robust.Shared.Map.MapCoordinates here, TimeSpan now)
    {
        foreach (var totem in _rule.GetTotems())
        {
            if (TryComp(totem, out BadBloodTotemComponent? comp) && comp.Registered && !comp.Destroyed &&
                !comp.Contested && !comp.Desecrated)
            {
                WarnAt(tracker, totem, comp.ContestRadius, "cmu-bad-blood-tracker-early-warning", now);
            }
        }

        // a Hunt Ground the clan has been ordered to seize warns the same way
        var grounds = EntityQueryEnumerator<BadBloodHuntGroundComponent>();
        while (grounds.MoveNext(out var ground, out var comp))
        {
            if (!comp.Seized && !comp.Contested)
                WarnAt(tracker, ground, comp.Radius, "cmu-bad-blood-tracker-early-warning-ground", now);
        }
    }

    private void WarnAt(Entity<BadBloodTrackerComponent> tracker, EntityUid site, float contestRadius, string message, TimeSpan now)
    {
        if (tracker.Comp.WarnedAt.TryGetValue(site, out var warned) && now - warned < tracker.Comp.WarningCooldown)
            return;

        var at = _transform.GetMapCoordinates(site);
        _marines.Clear();
        var radius = contestRadius + tracker.Comp.WarningPadding;
        var range = radius * radius;
        var marines = EntityQueryEnumerator<MarineComponent, TransformComponent>();
        while (marines.MoveNext(out var marine, out _, out var xform))
        {
            if (xform.MapID == at.MapId &&
                _mobState.IsAlive(marine) &&
                !_badBlood.IsBadBlood(marine) &&
                (_transform.GetWorldPosition(xform) - at.Position).LengthSquared() <= range)
            {
                _marines.Add(marine);
            }
        }

        if (_marines.Count == 0)
            return;

        tracker.Comp.WarnedAt[site] = now;
        EarlyWarningsSent++;
        foreach (var marine in _marines)
        {
            _preyTracking.Track(marine, tracker.Comp.BloodTrailShowFor);
        }

        _popup.PopupEntity(Loc.GetString(message, ("count", _marines.Count), ("area", _area.GetAreaName(site))),
            tracker, tracker, PopupType.MediumCaution);
    }

    private void OnGetArmor(Entity<BadBloodTrackerComponent> ent, ref CMGetArmorEvent args)
    {
        args.ArmorModifier *= ent.Comp.ArmorMultiplier;
    }

    private void OnGetVerbs(GetVerbsEvent<Verb> args)
    {
        if (!args.CanInteract ||
            args.User == args.Target ||
            !TryComp(args.User, out BadBloodTrackerComponent? tracker) ||
            !HasComp<MobStateComponent>(args.Target) ||
            HasComp<YautjaComponent>(args.Target) ||
            _mobState.IsDead(args.Target))
        {
            return;
        }

        var user = args.User;
        var target = args.Target;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("cmu-bad-blood-tracker-mark-verb"),
            Message = Loc.GetString("cmu-bad-blood-tracker-mark-message", ("seconds", (int) tracker.MarkDuration.TotalSeconds)),
            Act = () => TryMarkForClan((user, tracker), target),
        });
    }

    /// <summary>
    /// Marks <paramref name="target"/> as the Tracker's prey and shows it on every Bad Blood's bracer map for the
    /// Tracker's MarkDuration. Marking their own prey again only renews the map.
    /// </summary>
    public bool TryMarkForClan(Entity<BadBloodTrackerComponent> tracker, EntityUid target)
    {
        if (!_power.TryGetWornBracer(tracker, out var bracer))
        {
            _popup.PopupEntity(Loc.GetString("cmu-bad-blood-tracker-no-bracer"), tracker, tracker, PopupType.SmallCaution);
            return false;
        }

        // the mark system tells the Tracker why a mark is refused
        if (!_mark.IsMarkedBy(target, YautjaMarkKind.Prey, tracker) &&
            !_mark.TryMark(bracer, tracker, target, YautjaMarkKind.Prey, null))
        {
            return false;
        }

        _preyTracking.Track(target, tracker.Comp.MarkDuration);
        _popup.PopupEntity(
            Loc.GetString("cmu-bad-blood-tracker-marked", ("seconds", (int) tracker.Comp.MarkDuration.TotalSeconds)),
            tracker,
            tracker,
            PopupType.Medium);
        return true;
    }
}
