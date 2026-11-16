using System.Diagnostics.CodeAnalysis;
using Content.Server.Administration.Logs;
using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Containers;

namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// FLOW-505 (I1): an intel-skilled marine decrypts the data-core of a Bad Blood bracer that no living Bad Blood wears.
/// It shows the Lodge on the marine map for a while, tells the marine where the clan ship stands, and turns the bracer
/// into retrieve intel. The ship-state report is shared with <see cref="BadBloodLodgeIntelSystem"/>.
/// </summary>
public sealed class BadBloodBracerDataCoreSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private BadBloodSystem _badBlood = default!;
    [Dependency] private BadBloodCampSystem _camp = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private IntelSystem _intel = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private BadBloodCampRuleSystem _rule = default!;
    [Dependency] private SkillsSystem _skills = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BadBloodBracerDataCoreComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<BadBloodBracerDataCoreComponent, BadBloodBracerDecryptDoAfterEvent>(OnDecryptDoAfter);
    }

    private void OnGetVerbs(Entity<BadBloodBracerDataCoreComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Decrypted || _badBlood.IsBadBlood(args.User))
            return;

        var user = args.User;
        var bracer = ent.Owner;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cmu-bad-blood-intel-decrypt-verb"),
            Message = Loc.GetString("cmu-bad-blood-intel-decrypt-verb-message"),
            Act = () => TryStartDecrypt(user, bracer),
        });
    }

    /// <summary>
    /// Whether <paramref name="user"/> may decrypt <paramref name="bracer"/> now. <paramref name="reason"/> is the
    /// locale id of the refusal.
    /// </summary>
    public bool CanDecrypt(EntityUid user, EntityUid bracer, [NotNullWhen(false)] out string? reason)
    {
        reason = null;
        if (!TryComp(bracer, out BadBloodBracerDataCoreComponent? core) || TerminatingOrDeleted(bracer))
        {
            reason = "cmu-bad-blood-intel-decrypt-not-core";
            return false;
        }

        if (core.Decrypted)
        {
            reason = "cmu-bad-blood-intel-decrypt-done";
            return false;
        }

        if (_badBlood.IsBadBlood(user))
        {
            reason = "cmu-bad-blood-intel-decrypt-clan";
            return false;
        }

        if (!_skills.HasSkill(user, core.Skill, core.SkillLevel))
        {
            reason = "cmu-bad-blood-intel-decrypt-unskilled";
            return false;
        }

        // the fallen Leader's bracer is about to blow
        if (HasComp<BadBloodScuttleComponent>(bracer))
        {
            reason = "cmu-bad-blood-intel-decrypt-scuttle";
            return false;
        }

        if (IsHeldByLivingBadBlood(bracer))
        {
            reason = "cmu-bad-blood-intel-decrypt-worn";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Starts the decryption do-after. Tells <paramref name="user"/> why when it is refused.
    /// </summary>
    public bool TryStartDecrypt(EntityUid user, EntityUid bracer)
    {
        if (!CanDecrypt(user, bracer, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason), user, user, PopupType.SmallCaution);
            return false;
        }

        var core = Comp<BadBloodBracerDataCoreComponent>(bracer);
        var doAfter = new DoAfterArgs(EntityManager, user, core.DecryptTime, new BadBloodBracerDecryptDoAfterEvent(),
            bracer, target: bracer)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-intel-decrypt-start"), user, user);
        return true;
    }

    private void OnDecryptDoAfter(Entity<BadBloodBracerDataCoreComponent> ent, ref BadBloodBracerDecryptDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        var user = args.User;

        // a Bad Blood can have put it back on, or the scuttle started, while the do-after ran
        if (!CanDecrypt(user, ent.Owner, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason), user, user, PopupType.SmallCaution);
            return;
        }

        ent.Comp.Decrypted = true;
        _camp.RevealLodge(ent.Comp.RevealTime, BadBloodRevealSource.Intel);
        _intel.MakeRetrieveIntel(ent.Owner, ent.Comp.RetrieveValue);

        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-intel-decrypt-finished", ("ship", DescribeShip())), user, user,
            PopupType.Medium);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"BadBlood obj: {ToPrettyString(user):player} decrypted the Bad Blood bracer {ToPrettyString(ent.Owner):bracer}");

        var ev = new BadBloodBracerDecryptedEvent(ent.Owner, user);
        RaiseLocalEvent(ref ev);
    }

    /// <summary>
    /// What a marine learns about the clan ship from Bad Blood intel: whether a call is coming and when it would be
    /// in orbit, how long until an inbound ship arrives, or how long an arrived ship stays.
    /// </summary>
    public string DescribeShip()
    {
        switch (_rule.GetShipState())
        {
            case BadBloodShipState.Inbound:
                return Loc.GetString("cmu-bad-blood-intel-ship-inbound", ("minutes", Minutes(_rule.GetShipCallEta())));
            case BadBloodShipState.Arrived:
                return Loc.GetString("cmu-bad-blood-intel-ship-arrived", ("minutes", Minutes(_rule.GetDepartureRemaining())));
        }

        return _rule.GetShipCallEta() is { } eta
            ? Loc.GetString("cmu-bad-blood-intel-ship-hold", ("minutes", Minutes(eta)))
            : Loc.GetString("cmu-bad-blood-intel-ship-quiet");
    }

    private static int Minutes(TimeSpan? time)
    {
        return Math.Max(1, (int) Math.Ceiling((time ?? TimeSpan.Zero).TotalMinutes));
    }

    // worn, held or stowed by a Bad Blood who is not dead, however deep it is packed
    private bool IsHeldByLivingBadBlood(EntityUid item)
    {
        var current = item;
        for (var depth = 0; depth < 8; depth++)
        {
            if (!_container.TryGetContainingContainer((current, null, null), out var container))
                return false;

            var owner = container.Owner;
            if (_badBlood.IsBadBlood(owner) && !_mobState.IsDead(owner))
                return true;

            current = owner;
        }

        return false;
    }
}
