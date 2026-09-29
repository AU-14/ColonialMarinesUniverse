using Content.Shared.CMU14.Medical.Core;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Verbs;
using Robust.Shared.Network;

namespace Content.Shared.CMU14.Medical.Treatment.Surgery;

/// <summary>
///     Lets a bystander hold a downed patient still so a surgeon can operate without steps failing.
/// </summary>
public sealed class CMUSurgeryHoldDownSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly TimeSpan HoldDelay = TimeSpan.FromSeconds(2);

    public override void Initialize()
    {
        // MobStateComponent's verb subscription belongs to the recovery position system.
        SubscribeLocalEvent<CMUHumanMedicalComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<HumanoidProfileComponent, CMUSurgeryHoldDownDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<CMUSurgeryHeldDownComponent, ExaminedEvent>(OnExamined);
    }

    /// <summary>
    ///     True while <paramref name="patient"/> is held by someone other than <paramref name="surgeon"/> who is still next to them.
    /// </summary>
    public bool IsHeldDownFor(EntityUid patient, EntityUid surgeon)
    {
        return TryComp<CMUSurgeryHeldDownComponent>(patient, out var held) &&
               held.Holder != surgeon &&
               IsHoldValid((patient, held));
    }

    private bool CanHold(EntityUid user, EntityUid target)
    {
        return user != target &&
               HasComp<HumanoidProfileComponent>(target) &&
               _standing.IsDown(target) &&
               !_mobState.IsIncapacitated(user) &&
               !HasComp<CMUSurgeryHeldDownComponent>(target);
    }

    private bool IsHoldValid(Entity<CMUSurgeryHeldDownComponent> ent)
    {
        var holder = ent.Comp.Holder;
        if (TerminatingOrDeleted(holder) || _mobState.IsIncapacitated(holder) || !_standing.IsDown(ent))
            return false;

        var holderXform = Transform(holder);
        var patientXform = Transform(ent);
        if (holderXform.MapID != patientXform.MapID)
            return false;

        var distance = (_transform.GetWorldPosition(holderXform) - _transform.GetWorldPosition(patientXform)).Length();
        return distance <= ent.Comp.MaxRange;
    }

    private void OnGetVerbs(Entity<CMUHumanMedicalComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        var target = ent.Owner;

        if (TryComp<CMUSurgeryHeldDownComponent>(target, out var held) && held.Holder == user)
        {
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString("cmu-surgery-hold-down-release-verb"),
                Act = () => Release(target, user),
            });
            return;
        }

        if (!CanHold(user, target))
            return;

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("cmu-surgery-hold-down-verb"),
            Act = () => StartHold(user, target),
        });
    }

    private void StartHold(EntityUid user, EntityUid target)
    {
        var doAfter = new DoAfterArgs(EntityManager, user, HoldDelay, new CMUSurgeryHoldDownDoAfterEvent(), target, target)
        {
            BreakOnMove = true,
            NeedHand = true,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _popup.PopupPredicted(Loc.GetString("cmu-surgery-hold-down-start-self", ("target", target)),
            Loc.GetString("cmu-surgery-hold-down-start-others", ("user", user), ("target", target)),
            target,
            user);
    }

    private void OnDoAfter(Entity<HumanoidProfileComponent> ent, ref CMUSurgeryHoldDownDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !CanHold(args.User, ent))
            return;

        args.Handled = true;

        if (_net.IsClient)
            return;

        var held = EnsureComp<CMUSurgeryHeldDownComponent>(ent);
        held.Holder = args.User;
        Dirty(ent, held);

        _popup.PopupEntity(Loc.GetString("cmu-surgery-hold-down-done", ("user", args.User), ("target", ent.Owner)), ent);
    }

    private void Release(EntityUid target, EntityUid user)
    {
        if (_net.IsClient)
            return;

        RemCompDeferred<CMUSurgeryHeldDownComponent>(target);
        _popup.PopupEntity(Loc.GetString("cmu-surgery-hold-down-released", ("user", user), ("target", target)), target);
    }

    private void OnExamined(Entity<CMUSurgeryHeldDownComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("cmu-surgery-hold-down-examine", ("target", ent.Owner), ("holder", ent.Comp.Holder)));
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<CMUSurgeryHeldDownComponent>();
        while (query.MoveNext(out var uid, out var held))
        {
            if (IsHoldValid((uid, held)))
                continue;

            RemCompDeferred<CMUSurgeryHeldDownComponent>(uid);
            _popup.PopupEntity(Loc.GetString("cmu-surgery-hold-down-lost", ("target", uid)), uid);
        }
    }
}
