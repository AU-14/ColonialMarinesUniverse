using Content.Shared.CMU14.Xenomorphs.Reaper;
using Content.Shared.Damage.Components;
using Content.Shared.DoAfter;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Spawners;

// Reaper's existing flesh accounting stays owned by the same system.
namespace Content.Shared._RMC14.Xenonids.Reaper;

public sealed partial class XenoReaperSystem
{
    [Dependency] private MobThresholdSystem _revivalThresholds = default!;

    private void InitializeCmuRevival()
    {
        SubscribeLocalEvent<XenoReaperComponent, CMUReaperReviveActionEvent>(OnReviveAction);
        SubscribeLocalEvent<XenoReaperComponent, CMUReaperReviveDoAfterEvent>(OnReviveDoAfter);
        SubscribeLocalEvent<XenoReaperComponent, DoAfterAttemptEvent<CMUReaperReviveDoAfterEvent>>(OnReviveAttempt);
    }

    private bool CanRevive(EntityUid caster, EntityUid target)
    {
        return !TerminatingOrDeleted(caster) && !TerminatingOrDeleted(target)
            && !EntityManager.IsQueuedForDeletion(target)
            && _mobState.IsAlive(caster) && _mobState.IsDead(target)
            && HasComp<XenoComponent>(target) && HasComp<DamageableComponent>(target)
            && HasComp<MobThresholdsComponent>(target) && _hive.FromSameHive(caster, target);
    }

    private void OnReviveAction(Entity<XenoReaperComponent> xeno, ref CMUReaperReviveActionEvent args)
    {
        if (args.Handled || _net.IsClient || !TryComp<CMUReaperRevivalComponent>(xeno, out var revival))
            return;

        if (!CanRevive(xeno, args.Target) || HasComp<CMUReaperRevivingComponent>(args.Target))
        {
            _popup.PopupEntity(Loc.GetString("cmu-reaper-revive-invalid"), xeno, xeno);
            return;
        }

        // Check freshness on admission. The accepted ritual preserves the body while charging.
        if (!TryComp<CMUReaperCorpseComponent>(args.Target, out var corpse)
            || _timing.CurTime - corpse.DiedAt > revival.MaximumDeathAge)
        {
            _popup.PopupEntity(Loc.GetString("cmu-reaper-revive-too-old"), xeno, xeno);
            return;
        }

        if (!HasFleshResinPopup(xeno, revival.FleshCost) || !_rmcActions.CanUseActionPopup(xeno, args.Action, args.Target))
            return;

        var effect = StartRitual(xeno, args.Target, revival.ChargeTime, CMUReaperRitualKind.Revival);
        var reserved = EnsureComp<CMUReaperRevivingComponent>(args.Target);
        reserved.Effect = effect;
        var doAfter = new DoAfterArgs(EntityManager, xeno, revival.ChargeTime,
            new CMUReaperReviveDoAfterEvent { Effect = GetNetEntity(effect) }, xeno, args.Target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            AttemptFrequency = AttemptFrequency.EveryTick,
            DuplicateCondition = DuplicateConditions.SameEvent,
            CancelDuplicate = false,
            DistanceThreshold = 1.5f,
        };
        if (!_doAfter.TryStartDoAfter(doAfter))
        {
            RemComp<CMUReaperRevivingComponent>(args.Target);
            QueueDel(effect);
            return;
        }

        // The accepted cast pays up front. Cancellation deliberately does not refund it.
        TryRemoveFleshResin(xeno, revival.FleshCost);
        args.Handled = true;
        var started = new CMUReaperRevivalStartedEvent();
        RaiseLocalEvent(args.Target, ref started);
        _audio.PlayPvs(xeno.Comp.FleshBloomSound, xeno);
        _popup.PopupEntity(Loc.GetString("cmu-reaper-revive-start"), args.Target, xeno);
    }

    private void OnReviveAttempt(Entity<XenoReaperComponent> xeno, ref DoAfterAttemptEvent<CMUReaperReviveDoAfterEvent> args)
    {
        if (_net.IsClient)
            return;

        if (args.DoAfter.Args.Target is not { } target || !CanRevive(xeno, target)
            || !TryComp<CMUReaperRevivingComponent>(target, out var reserved)
            || reserved.Effect != GetEntity(args.Event.Effect))
            args.Cancel();
    }

    private void OnReviveDoAfter(Entity<XenoReaperComponent> xeno, ref CMUReaperReviveDoAfterEvent args)
    {
        if (_net.IsClient || args.Handled)
            return;

        args.Handled = true;
        var effect = GetEntity(args.Effect);
        var success = false;
        if (!args.Cancelled && args.Target is { } target && CanRevive(xeno, target)
            && TryComp<CMUReaperRevivingComponent>(target, out var reserved) && reserved.Effect == effect)
        {
            // Restore health through the normal threshold/state pipeline; retain the body's mind and caste.
            var thresholds = Comp<MobThresholdsComponent>(target);
            var allowRevives = thresholds.AllowRevives;
            _damageable.ClearAllDamage(target);
            _revivalThresholds.SetAllowRevives(target, true, thresholds);
            _revivalThresholds.SetAllowRevives(target, allowRevives, thresholds);
            success = _mobState.IsAlive(target);
            if (success)
            {
                _popup.PopupEntity(Loc.GetString("cmu-reaper-revive-complete"), target, xeno);
                var revived = new CMUReaperRevivedEvent();
                RaiseLocalEvent(target, ref revived);
            }
        }

        FinishRitual(effect, success);
        if (!success)
            _popup.PopupEntity(Loc.GetString("cmu-reaper-revive-cancelled"), xeno, xeno);
    }

    private EntityUid StartRitual(EntityUid caster, EntityUid target, TimeSpan duration, CMUReaperRitualKind kind)
    {
        var coordinates = Transform(target).Coordinates;
        var effect = SpawnAtPosition("CMUEffectReaperRitual", coordinates);
        var visual = Comp<CMUReaperRitualComponent>(effect);
        visual.Caster = caster;
        visual.Target = target;
        visual.Origin = _transform.WithEntityId(Transform(caster).Coordinates, coordinates.EntityId).Position - coordinates.Position;
        visual.StartedAt = _timing.CurTime;
        visual.Duration = duration;
        visual.Kind = kind;
        Dirty(effect, visual);
        EnsureComp<TimedDespawnComponent>(effect).Lifetime = (float) duration.TotalSeconds + 3;
        return effect;
    }

    private void FinishRitual(EntityUid effect, bool success)
    {
        if (!TryComp<CMUReaperRitualComponent>(effect, out var visual) || visual.State != CMUReaperCastState.Charging)
            return;

        visual.State = success ? CMUReaperCastState.Succeeded : CMUReaperCastState.Cancelled;
        visual.EndedAt = _timing.CurTime;
        Dirty(effect, visual);
        EnsureComp<TimedDespawnComponent>(effect).Lifetime = 2;
        if (TryComp<CMUReaperRevivingComponent>(visual.Target, out var reserved) && reserved.Effect == effect)
            RemComp<CMUReaperRevivingComponent>(visual.Target);
    }

    private void UpdateCmuRituals()
    {
        var blooms = EntityQueryEnumerator<CMUFleshBloomTelegraphComponent>();
        while (blooms.MoveNext(out var uid, out var bloom))
        {
            if (bloom.State == CMUReaperCastState.Charging
                && (TerminatingOrDeleted(bloom.Caster) || !_mobState.IsAlive(bloom.Caster)
                    || _timing.CurTime > bloom.StartedAt + bloom.Duration + TimeSpan.FromSeconds(1)))
                FinishBloom(uid, false);
        }

        var query = EntityQueryEnumerator<CMUReaperRitualComponent>();
        while (query.MoveNext(out var uid, out var ritual))
        {
            if (ritual.State != CMUReaperCastState.Charging)
                continue;

            // The caster can disappear before its do-after callback is delivered.
            if (TerminatingOrDeleted(ritual.Caster) || TerminatingOrDeleted(ritual.Target)
                || !HasComp<XenoReaperComponent>(ritual.Caster)
                || !_mobState.IsAlive(ritual.Caster)
                || _timing.CurTime > ritual.StartedAt + ritual.Duration + TimeSpan.FromSeconds(1))
                FinishRitual(uid, false);
        }
    }

    private void FinishBloom(EntityUid effect, bool success)
    {
        if (!TryComp<CMUFleshBloomTelegraphComponent>(effect, out var bloom) || bloom.State != CMUReaperCastState.Charging)
            return;

        bloom.State = success ? CMUReaperCastState.Succeeded : CMUReaperCastState.Cancelled;
        bloom.EndedAt = _timing.CurTime;
        Dirty(effect, bloom);
        EnsureComp<TimedDespawnComponent>(effect).Lifetime = success ? 1.6f : 0.8f;
    }
}
