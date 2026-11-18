using Content.Server.CMU14.TacticalMap;
using Content.Shared._RMC14.Armor;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;

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

    public override void Initialize()
    {
        SubscribeLocalEvent<BadBloodTrackerComponent, CMGetArmorEvent>(OnGetArmor);
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);
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
