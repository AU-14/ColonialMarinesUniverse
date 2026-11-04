using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Verbs;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Medical.RecoveryPosition;

public sealed partial class CMURecoveryPositionSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private RMCUnrevivableSystem _unrevivable = default!;

    private static readonly ProtoId<DamageTypePrototype> Asphyxiation = "Asphyxiation";
    private static readonly TimeSpan RollDelay = TimeSpan.FromSeconds(2);

    public override void Initialize()
    {
        SubscribeLocalEvent<MobStateComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<HumanoidProfileComponent, CMURecoveryPositionDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<CMURecoveryPositionComponent, MoveInputEvent>(OnMoveInput);
        SubscribeLocalEvent<CMURecoveryPositionComponent, ExaminedEvent>(OnExamined);
    }

    private bool CanRoll(EntityUid user, EntityUid target)
    {
        return user != target &&
               HasComp<HumanoidProfileComponent>(target) &&
               _standing.IsDown(target) &&
               !HasComp<CMURecoveryPositionComponent>(target);
    }

    private void OnGetVerbs(Entity<MobStateComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !CanRoll(args.User, ent))
            return;

        var user = args.User;
        var target = ent.Owner;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("cmu-recovery-position-verb"),
            Act = () => StartRoll(user, target),
        });
    }

    private void StartRoll(EntityUid user, EntityUid target)
    {
        var doAfter = new DoAfterArgs(EntityManager, user, RollDelay, new CMURecoveryPositionDoAfterEvent(), target, target)
        {
            BreakOnMove = true,
            NeedHand = true,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _popup.PopupEntity(Loc.GetString("cmu-recovery-position-start-self", ("target", target)),
            Loc.GetString("cmu-recovery-position-start-others", ("user", user), ("target", target)),
            target,
            user);
    }

    private void OnDoAfter(Entity<HumanoidProfileComponent> ent, ref CMURecoveryPositionDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !CanRoll(args.User, ent))
            return;

        args.Handled = true;

        if (_net.IsClient)
            return;

        var targetPos = _transform.GetWorldPosition(ent);
        var userPos = _transform.GetWorldPosition(args.User);
        var side = userPos.X >= targetPos.X ? Direction.East : Direction.West;
        _transform.SetWorldRotation(ent, side.ToAngle());

        var recovery = EnsureComp<CMURecoveryPositionComponent>(ent);
        recovery.Position = targetPos;
        recovery.NextHeal = _timing.CurTime + recovery.HealInterval;
        recovery.NextRotBonus = _timing.CurTime + recovery.RotBonusInterval;
        Dirty(ent, recovery);

        _popup.PopupEntity(Loc.GetString("cmu-recovery-position-done-self", ("target", ent.Owner)), ent, args.User);
        _popup.PopupEntity(Loc.GetString("cmu-recovery-position-done-others", ("user", args.User), ("target", ent.Owner)),
            ent,
            Filter.PvsExcept(args.User),
            true);
    }

    private void OnMoveInput(Entity<CMURecoveryPositionComponent> ent, ref MoveInputEvent args)
    {
        if (_net.IsClient || !args.HasDirectionalMovement)
            return;

        RemCompDeferred<CMURecoveryPositionComponent>(ent);
    }

    private void OnExamined(Entity<CMURecoveryPositionComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("cmu-recovery-position-examine", ("target", ent.Owner)));
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMURecoveryPositionComponent>();
        while (query.MoveNext(out var uid, out var recovery))
        {
            if (!_standing.IsDown(uid) ||
                TryComp<PullableComponent>(uid, out var pullable) && pullable.BeingPulled ||
                (_transform.GetWorldPosition(uid) - recovery.Position).Length() > recovery.MoveTolerance)
            {
                RemCompDeferred<CMURecoveryPositionComponent>(uid);
                continue;
            }

            if (!_mobState.IsDead(uid))
            {
                recovery.NextRotBonus = now + recovery.RotBonusInterval;
            }
            else if (now >= recovery.NextRotBonus)
            {
                recovery.NextRotBonus = now + recovery.RotBonusInterval;
                _unrevivable.AddRevivableTime(uid, recovery.RotBonus);
            }

            if (now < recovery.NextHeal || !_mobState.IsAlive(uid))
                continue;

            recovery.NextHeal = now + recovery.HealInterval;
            var heal = new DamageSpecifier { DamageDict = { [Asphyxiation] = -recovery.HealAmount } };
            _damageable.TryChangeDamage(uid, heal, true, false);
        }
    }
}
