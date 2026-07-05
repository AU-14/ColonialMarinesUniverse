using System.Collections.Generic;
using Content.Server.AU14.ColonyEconomy;
using Content.Server.Stack;
using Content.Shared._AU14.Insurgency.Sapper;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.Access.Components;
using Content.Shared.AU14.ColonyEconomy;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._AU14.Insurgency.Sapper;

/// <summary>
///     Runs the financial siphon rig. Clamping it onto a colony finance device (ATM, budget console, or
///     ASRS terminal) runs a long, loud do-after; on completion it drains the appropriate fictional funds
///     into cash and, for ATMs, leaves the machine temporarily disrupted (sparking, refusing to open) until
///     it self-repairs after five minutes.
///
///     ATM payout: an equal slice of the whole managed pool - the colony budget plus every department's
///     budget - divided across the ATMs, plus a 5% skim from every player account. Budget console and ASRS
///     terminal instead drain their own held funds in full, with heavier feedback for the bigger event.
/// </summary>
public sealed class SapperAtmHackingSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private ColonyBudgetSystem _budget = default!;
    [Dependency] private SharedRequisitionsSystem _requisitions = default!;
    [Dependency] private IGameTiming _timing = default!;

    // Loud one-shot for the big console/ASRS drains so the theft reads as a major event.
    private static readonly SoundSpecifier BigDrainSound =
        new SoundPathSpecifier("/Audio/Machines/circuitprinter.ogg", AudioParams.Default.WithVolume(6f).WithMaxDistance(20f));

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SapperAtmHackingComponent, AfterInteractEvent>(OnToolAfterInteract);
        SubscribeLocalEvent<SapperAtmHackingComponent, SapperAtmHackDoAfterEvent>(OnHackFinished);

        // Registered before the real ATM system so a disrupted machine buzzes instead of opening.
        SubscribeLocalEvent<SapperAtmHackedComponent, InteractUsingEvent>(OnHackedInteractUsing, before: new[] { typeof(ColonyAtmSystem) });
    }

    // Only the three finance-device types are valid siphon targets.
    private bool IsFinanceTarget(EntityUid target) =>
        HasComp<ColonyAtmComponent>(target) ||
        HasComp<BudgetConsoleComponent>(target) ||
        HasComp<RequisitionsComputerComponent>(target);

    private void OnToolAfterInteract(Entity<SapperAtmHackingComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (!IsFinanceTarget(target))
            return;

        args.Handled = true;

        if (!HasComp<SapperComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("insfor-sapper-trap-unskilled"), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        if (HasComp<SapperAtmHackedComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("insfor-sapper-atm-already-hacked"), target, args.User, PopupType.SmallCaution);
            return;
        }

        // Loud from the first second: the rig grinding into the machine is the counterplay window.
        if (ent.Comp.StartSound is { } start)
            _audio.PlayPvs(start, target);

        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.HackTime, new SapperAtmHackDoAfterEvent(), ent, target, ent)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnHandChange = true,
        };
        if (_doAfter.TryStartDoAfter(doAfter))
        {
            // Sparks fly for the whole siphon, not just afterwards: the machine visibly fights back
            // while the rig grinds into it.
            var siphoning = EnsureComp<SapperAtmSiphoningComponent>(target);
            siphoning.NextSpark = _timing.CurTime;
        }
    }

    private void OnHackFinished(Entity<SapperAtmHackingComponent> ent, ref SapperAtmHackDoAfterEvent args)
    {
        if (args.Handled || args.Target is not { } target)
            return;

        // Whether it finished or broke, the "being siphoned" spark state ends here.
        RemComp<SapperAtmSiphoningComponent>(target);

        if (args.Cancelled)
            return;

        args.Handled = true;

        if (HasComp<ColonyAtmComponent>(target))
            DrainAtm(ent, target, args.User);
        else if (HasComp<BudgetConsoleComponent>(target))
            DrainBudgetConsole(ent, target, args.User);
        else if (HasComp<RequisitionsComputerComponent>(target))
            DrainAsrs(ent, target, args.User);
    }

    // ----- ATM: shared slice of the whole pool + a 5% account skim, then temporary disruption ----------

    private void DrainAtm(Entity<SapperAtmHackingComponent> ent, EntityUid atm, EntityUid user)
    {
        if (HasComp<SapperAtmHackedComponent>(atm))
            return;

        // The managed pool is the colony budget plus every department's budget (departments deduped by id,
        // since several consoles share one department).
        var pool = _budget.GetBudget();
        var seenDepartments = new HashSet<string>();
        var deptQuery = EntityQueryEnumerator<DepartmentConsoleComponent>();
        while (deptQuery.MoveNext(out _, out var dept))
        {
            if (dept.DepartmentId is { } deptId && !seenDepartments.Add(deptId))
                continue;
            pool += dept.DepartmentBudget;
        }

        var unhacked = 0;
        var atmQuery = EntityQueryEnumerator<ColonyAtmComponent>();
        while (atmQuery.MoveNext(out var uid, out _))
        {
            if (!HasComp<SapperAtmHackedComponent>(uid))
                unhacked++;
        }

        // The pool only SIZES the slice; the actual debit comes off the colony budget, so clamp the
        // payout to what that budget really holds. Without the clamp the slice could exceed the
        // colony budget (departments hold most of the pool), driving it negative and minting cash
        // that was never removed from any department.
        var payout = unhacked > 0 ? (int) (pool / unhacked) : 0;
        payout = System.Math.Min(payout, System.Math.Max(0, (int) _budget.GetBudget()));
        if (payout > 0)
            _budget.AddToBudget(-payout);

        // Skim 5% off every player account and add it to the haul.
        payout += SkimAccounts(0.05f);

        if (payout > 0)
            _stack.SpawnMultiple(ent.Comp.CashPrototype, payout, atm);

        Disrupt(atm);
        if (ent.Comp.SuccessSound is { } success)
            _audio.PlayPvs(success, atm);
        _popup.PopupEntity(Loc.GetString("insfor-sapper-atm-hacked", ("amount", payout)), atm, user);
    }

    // ----- budget console: drain the colony budget in full, big feedback ------------------------------

    private void DrainBudgetConsole(Entity<SapperAtmHackingComponent> ent, EntityUid console, EntityUid user)
    {
        var funds = (int) _budget.GetBudget();
        if (funds > 0)
        {
            _budget.AddToBudget(-funds);
            _stack.SpawnMultiple(ent.Comp.CashPrototype, funds, console);
        }

        Disrupt(console);
        BigDrainFeedback(console);
        _popup.PopupEntity(Loc.GetString("insfor-sapper-console-drained", ("amount", funds)), console, user, PopupType.LargeCaution);
    }

    // ----- ASRS terminal: drain the requisitions account it is wired to -------------------------------

    private void DrainAsrs(Entity<SapperAtmHackingComponent> ent, EntityUid computer, EntityUid user)
    {
        if (!TryComp(computer, out RequisitionsComputerComponent? comp) ||
            comp.Account is not { } account ||
            !TryComp(account, out RequisitionsAccountComponent? accountComp))
        {
            _popup.PopupEntity(Loc.GetString("insfor-sapper-asrs-empty"), computer, user, PopupType.SmallCaution);
            return;
        }

        var funds = accountComp.Balance;
        if (funds > 0)
        {
            _requisitions.ChangeBudget(-funds, accountComp.Faction);
            _stack.SpawnMultiple(ent.Comp.CashPrototype, funds, computer);
        }

        Disrupt(computer);
        BigDrainFeedback(computer);
        _popup.PopupEntity(Loc.GetString("insfor-sapper-asrs-drained", ("amount", funds)), computer, user, PopupType.LargeCaution);
    }

    // ----- helpers ------------------------------------------------------------------------------------

    // Takes the given fraction off every player account balance and returns the total skimmed.
    private int SkimAccounts(float fraction)
    {
        var total = 0;
        var query = EntityQueryEnumerator<IdCardComponent>();
        while (query.MoveNext(out var uid, out var card))
        {
            if (card.AccountBalance <= 0)
                continue;

            var skim = (int) (card.AccountBalance * fraction);
            if (skim <= 0)
                continue;

            card.AccountBalance -= skim;
            Dirty(uid, card);
            total += skim;
        }

        return total;
    }

    // Puts an ATM into the temporary disrupted state (sparking, refusing to open) that repairs itself later.
    private void Disrupt(EntityUid atm)
    {
        var comp = EnsureComp<SapperAtmHackedComponent>(atm);
        comp.RecoverAt = _timing.CurTime + comp.RecoverDelay;
        comp.NextSpark = _timing.CurTime;
    }

    // Heavier one-shot presentation for the console/ASRS drains: sparks and a loud grind.
    private void BigDrainFeedback(EntityUid target)
    {
        var coords = Transform(target).Coordinates;
        Spawn("EffectSparks", coords);
        _audio.PlayPvs(BigDrainSound, target);
    }

    // A disrupted machine will not open; anyone using it gets the malfunction message and a buzz.
    private void OnHackedInteractUsing(Entity<SapperAtmHackedComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _audio.PlayPvs(ent.Comp.BuzzSound, ent);
        _popup.PopupEntity(Loc.GetString("insfor-sapper-atm-malfunction"), ent, args.User, PopupType.LargeCaution);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        // Sparks while a siphon do-after is actively running against the machine.
        var siphonQuery = EntityQueryEnumerator<SapperAtmSiphoningComponent>();
        while (siphonQuery.MoveNext(out var siphonUid, out var siphoning))
        {
            if (now < siphoning.NextSpark)
                continue;

            siphoning.NextSpark = now + TimeSpan.FromSeconds(siphoning.SparkInterval);
            Spawn(siphoning.SparkEffect, Transform(siphonUid).Coordinates);
        }

        var query = EntityQueryEnumerator<SapperAtmHackedComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            // Self-repair once the disruption window elapses.
            if (now >= comp.RecoverAt)
            {
                RemComp<SapperAtmHackedComponent>(uid);
                continue;
            }

            // Visible sparks every few seconds so the abnormal state is obvious.
            if (now >= comp.NextSpark)
            {
                comp.NextSpark = now + TimeSpan.FromSeconds(comp.SparkInterval);
                Spawn(comp.SparkEffect, Transform(uid).Coordinates);
            }
        }
    }
}
