using System.Diagnostics.CodeAnalysis;
using Content.Server.Administration.Logs;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Shared.Containers;

namespace Content.Server.CMU14.Yautja.BadBlood;

// The trophy offering: a Bad Blood hangs a prepared trophy on the Lodge and the clan is paid for it.
// Dependency fields taken by this file: _offeringAdminLog, _offeringContainers, _offeringDoAfter.
public sealed partial class BadBloodCampSystem
{
    [Dependency] private IAdminLogManager _offeringAdminLog = default!;
    [Dependency] private SharedContainerSystem _offeringContainers = default!;
    [Dependency] private SharedDoAfterSystem _offeringDoAfter = default!;

    partial void InitializeOffering()
    {
        SubscribeLocalEvent<BadBloodCampOfferingComponent, InteractUsingEvent>(OnOfferingInteractUsing);
        SubscribeLocalEvent<BadBloodCampOfferingComponent, BadBloodOfferTrophyDoAfterEvent>(OnOfferingDoAfter);
        SubscribeLocalEvent<BadBloodCampOfferingComponent, ExaminedEvent>(OnOfferingExamined);
    }

    // lodge.yml is the core's, so the Lodge gets its offering component here instead of in the prototype
    partial void LodgePitchedOffering(Entity<BadBloodCampComponent> lodge, Entity<BadBloodCampRuleComponent> rule, bool first)
    {
        var offering = EnsureComp<BadBloodCampOfferingComponent>(lodge.Owner);
        _offeringContainers.EnsureContainer<Container>(lodge.Owner, offering.ContainerId);
    }

    // runs while the Lodge still exists. a Lodge that is deleted outright takes its trophies with it
    partial void LodgeDestroyedOffering(Entity<BadBloodCampComponent> lodge, Entity<BadBloodCampRuleComponent> rule)
    {
        if (!TryComp(lodge.Owner, out BadBloodCampOfferingComponent? offering) ||
            MetaData(lodge.Owner).EntityLifeStage >= EntityLifeStage.Terminating ||
            !_offeringContainers.TryGetContainer(lodge.Owner, offering.ContainerId, out var container))
        {
            return;
        }

        _offeringContainers.EmptyContainer(container, true, Transform(lodge.Owner).Coordinates);
    }

    /// <summary>
    /// Whether the Lodge would take <paramref name="trophy"/>: a single prepared trophy, cut from a body that was
    /// killed and was not a Yautja, synth, larva or parasite, whose body has not given its two yet. The prey's
    /// faction does not matter, and neither does whether a player ever controlled it.
    /// <paramref name="reason"/> is the locale id of the refusal.
    /// </summary>
    public bool CanOfferTrophy(EntityUid trophy, [NotNullWhen(false)] out string? reason)
    {
        reason = null;
        if (!TryComp(trophy, out YautjaTrophyComponent? comp))
        {
            reason = "cmu-bad-blood-offering-not-trophy";
            return false;
        }

        if (HasComp<BadBloodOfferedTrophyComponent>(trophy))
        {
            reason = "cmu-bad-blood-offering-already-offered";
            return false;
        }

        // a stack only carries one body's stamp and one cured flag, so it is offered one piece at a time
        if (TryComp(trophy, out StackComponent? stack) && stack.Count != 1)
        {
            reason = "cmu-bad-blood-offering-stack";
            return false;
        }

        if (!comp.Polished)
        {
            reason = "cmu-bad-blood-offering-unprepared";
            return false;
        }

        if (!TryComp(trophy, out BadBloodTrophyOriginComponent? origin))
        {
            reason = "cmu-bad-blood-offering-unknown";
            return false;
        }

        if (!origin.WasKill || origin.IsYautja || origin.IsSynth || origin.IsLarva)
        {
            reason = "cmu-bad-blood-offering-unworthy";
            return false;
        }

        if (!_rule.CanOffer(origin.Body))
        {
            reason = "cmu-bad-blood-offering-body-spent";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Starts the offering do-after at the standing Lodge. Tells <paramref name="user"/> why when it is refused.
    /// </summary>
    public bool TryStartOffering(EntityUid user, EntityUid trophy)
    {
        if (!_badBlood.IsBadBlood(user) ||
            !TryGetLodge(out var lodge) ||
            !TryComp(lodge.Owner, out BadBloodCampOfferingComponent? offering))
        {
            return false;
        }

        if (!CanOfferTrophy(trophy, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason, ("trophy", trophy)), user, user, PopupType.SmallCaution);
            return false;
        }

        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            offering.OfferTime,
            new BadBloodOfferTrophyDoAfterEvent(),
            lodge.Owner,
            target: lodge.Owner,
            used: trophy)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnHandChange = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
        };

        if (!_offeringDoAfter.TryStartDoAfter(doAfter))
            return false;

        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-offering-start", ("trophy", trophy)), user, user);
        return true;
    }

    /// <summary>
    /// Hangs <paramref name="trophy"/> on the standing Lodge at once and pays the clan for it. False, with nothing
    /// taken or paid, if the Lodge would not take it.
    /// </summary>
    public bool TryOfferTrophy(EntityUid user, EntityUid trophy)
    {
        if (!_badBlood.IsBadBlood(user) ||
            !_rule.TryGetRule(out var rule) ||
            !TryGetLodge(out var lodge) ||
            !TryComp(lodge.Owner, out BadBloodCampOfferingComponent? offering) ||
            !CanOfferTrophy(trophy, out _) ||
            !TryComp(trophy, out BadBloodTrophyOriginComponent? origin))
        {
            return false;
        }

        var container = _offeringContainers.EnsureContainer<Container>(lodge.Owner, offering.ContainerId);
        if (!_offeringContainers.Insert(trophy, container))
            return false;

        EnsureComp<BadBloodOfferedTrophyComponent>(trophy);
        if (!_rule.TryCreditOffering(origin.Body))
            return false;

        _popup.PopupEntity(
            Loc.GetString("cmu-bad-blood-offering-finished", ("trophy", trophy), ("sp", rule.Comp.OfferingSp.ToString())),
            user,
            user);
        _offeringAdminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(user):player} offered Yautja trophy {ToPrettyString(trophy):trophy} at the Bad Blood Lodge {ToPrettyString(lodge.Owner):lodge} for {rule.Comp.OfferingSp} SP, cut from body {origin.Body}");
        return true;
    }

    /// <summary>
    /// How many trophies hang on the standing Lodge.
    /// </summary>
    public int GetOfferedTrophyCount()
    {
        if (!TryGetLodge(out var lodge) ||
            !TryComp(lodge.Owner, out BadBloodCampOfferingComponent? offering) ||
            !_offeringContainers.TryGetContainer(lodge.Owner, offering.ContainerId, out var container))
        {
            return 0;
        }

        return container.ContainedEntities.Count;
    }

    private void OnOfferingInteractUsing(Entity<BadBloodCampOfferingComponent> ent, ref InteractUsingEvent args)
    {
        // anyone else's trophy is left to whatever the click would have done
        if (args.Handled ||
            !HasComp<YautjaTrophyComponent>(args.Used) ||
            !_badBlood.IsBadBlood(args.User) ||
            _rule.GetLodge() != ent.Owner)
        {
            return;
        }

        args.Handled = true;
        TryStartOffering(args.User, args.Used);
    }

    private void OnOfferingDoAfter(Entity<BadBloodCampOfferingComponent> ent, ref BadBloodOfferTrophyDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Used is not { } trophy)
            return;

        args.Handled = true;

        if (_rule.GetLodge() != ent.Owner || TerminatingOrDeleted(trophy))
            return;

        // the trophy or its body's count can have changed while the do-after ran
        if (!CanOfferTrophy(trophy, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason, ("trophy", trophy)), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        TryOfferTrophy(args.User, trophy);
    }

    private void OnOfferingExamined(Entity<BadBloodCampOfferingComponent> ent, ref ExaminedEvent args)
    {
        var count = _offeringContainers.TryGetContainer(ent.Owner, ent.Comp.ContainerId, out var container)
            ? container.ContainedEntities.Count
            : 0;

        args.PushMarkup(Loc.GetString("cmu-bad-blood-offering-examine", ("count", count)));

        if (_badBlood.IsBadBlood(args.Examiner) && _rule.IsRuleActive())
            args.PushMarkup(Loc.GetString("cmu-bad-blood-offering-examine-sp", ("sp", FormatSp(_rule.GetSp()))));
    }
}
