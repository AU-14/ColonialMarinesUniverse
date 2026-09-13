using Content.Shared.ActionBlocker;
using Content.Shared.Chat;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Weapons.Misc;
using Content.Shared.Whitelist;
using JetBrains.Annotations;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared.Chasm;

/// <summary>
/// Handles making entities fall into chasms when stepped on.
/// </summary>
public sealed partial class ChasmSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedChatSystem _chat = default!;
    [Dependency] private SharedGrapplingGunSystem _grapple = default!;

    [Dependency] private EntityQuery<ChasmComponent> _chasmQuery;
    [Dependency] private EntityQuery<ChasmFallingComponent> _chasmFallingQuery;

    /// <inheritdoc />
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ChasmFallingComponent>();
        while (query.MoveNext(out var uid, out var chasm))
        {
            if (_timing.CurTime < chasm.NextDeletionTime)
                continue;

            // Deleting a replicated player tree must be authoritative. Predicting the deletion
            // dirties every networked child and can mutate component state during prediction reset.
            if (!_net.IsServer)
                continue;

            // RMC14: Complete the authoritative death transition before the chasm completion
            // events so queen-death listeners observe it before the falling entity is deleted.
            if (TryComp(uid, out MobStateComponent? mobState))
                _mobState.ChangeMobState(uid, MobState.Dead, mobState);

            // CMU z-level fallback falls have no physical chasm entity to receive completion events.
            if (chasm.FallingInto.IsValid())
            {
                var chasmEvent = new EntityCompletedFallingIntoChasmEvent((uid, chasm));
                RaiseLocalEvent(chasm.FallingInto, ref chasmEvent);
                if (_chasmQuery.TryComp(chasm.FallingInto, out var chasmComp))
                {
                    var tripperEvent = new CompletedFallingIntoChasmEvent((chasm.FallingInto, chasmComp));
                    RaiseLocalEvent(uid, ref tripperEvent);
                }
                else
                {
                    DebugTools.Assert($"{ToPrettyString(chasm.FallingInto)} is missing {nameof(ChasmComponent)}");
                }
            }

            QueueDeleteTree(uid);
        }
    }

    private void QueueDeleteTree(EntityUid uid)
    {
        var deletionOrder = new List<EntityUid>();
        AppendLeafFirst(uid, deletionOrder);

        foreach (var entity in deletionOrder)
        {
            QueueDel(entity);
        }
    }

    private void AppendLeafFirst(EntityUid uid, List<EntityUid> deletionOrder)
    {
        if (TerminatingOrDeleted(uid) || EntityManager.IsQueuedForDeletion(uid))
            return;

        if (TryComp(uid, out TransformComponent? transform))
        {
            var children = new List<EntityUid>(transform.ChildCount);
            var enumerator = transform.ChildEnumerator;
            while (enumerator.MoveNext(out var child))
            {
                children.Add(child);
            }

            foreach (var child in children)
            {
                AppendLeafFirst(child, deletionOrder);
            }
        }

        deletionOrder.Add(uid);
    }

    #region Event Handlers
    [SubscribeLocalEvent]
    private void OnStepTriggered(Entity<ChasmComponent> entity, ref StepTriggeredOffEvent args)
    {
        // already doomed
        if (_chasmFallingQuery.HasComp(args.Tripper))
            return;

        // Check the white-/blacklists and inform on rejection.
        if (!(entity.Comp.Whitelist == null && entity.Comp.Blacklist == null ||
              _whitelist.CheckBoth(args.Tripper, entity.Comp.Blacklist, entity.Comp.Whitelist)))
        {
            var rejected = new FallerRejectedByChasmEvent(args.Tripper);
            RaiseLocalEvent(entity, ref rejected);
            return;
        }

        // Give an opportunity to cancel the fall for whatever reason.
        var checkEvent = new EntityStartFallingAttemptEvent(args.Tripper);
        RaiseLocalEvent(entity, ref checkEvent);
        if (checkEvent.Cancelled)
            return;

        StartFalling(entity.AsNullable(), args.Tripper);
    }

    [SubscribeLocalEvent]
    private void OnStepTriggerAttempt(Entity<ChasmComponent> entity, ref StepTriggerAttemptEvent args)
    {
        if (_grapple.IsEntityHooked(args.Tripper))
        {
            args.Cancelled = true;
            return;
        }

        args.Continue = true;
    }

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<ChasmComponent> entity, ref ComponentShutdown args)
    {
        var e = EntityQueryEnumerator<ChasmFallingComponent>();
        while (e.MoveNext(out var fallingEnt, out var falling))
        {
            if (falling.FallingInto != entity.Owner)
                continue;

            RemCompDeferred<ChasmFallingComponent>(fallingEnt);
        }
    }

    [SubscribeLocalEvent]
    private static void OnUpdateCanMove(Entity<ChasmFallingComponent> entity, ref UpdateCanMoveEvent args)
    {
        args.Cancel();
    }
    #endregion Event Handlers

    #region Public API
    /// <summary>
    /// Causes <paramref name="tripper"/> to fall into <paramref name="chasm"/>: starts a falling animation, optionally
    /// plays a sound, and eventually deletes <paramref name="tripper"/>.
    /// If <paramref name="chasm"/> does not have a <see cref="ChasmComponent"/> component, does nothing and returns null.
    /// </summary>
    /// <param name="playSound">Whether or not the chasm should play a sound when the entity falls in.</param>
    /// <param name="playEmote">Whether or not <paramref name="tripper"/> should try to emote when falling into the chasm.</param>
    /// <returns>
    /// <paramref name="tripper"/> with its new <see cref="ChasmFallingComponent"/>, if the entity did start falling, null otherwise.
    /// </returns>
    [PublicAPI]
    public Entity<ChasmFallingComponent>? StartFalling(
        Entity<ChasmComponent?> chasm,
        EntityUid tripper,
        bool playSound = true,
        bool playEmote = true
    )
    {
        if (!_chasmQuery.Resolve(chasm, ref chasm.Comp, logMissing: false))
            return null;

        var falling = AddComp<ChasmFallingComponent>(tripper);
        falling.FallingInto = chasm;

        falling.NextDeletionTime = _timing.CurTime + falling.DeletionTime;
        _blocker.UpdateCanMove(tripper);

        if (playSound)
            _audio.PlayPredicted(chasm.Comp.FallingSound, chasm, tripper);

        if (playEmote && chasm.Comp.Emote is { } emote)
            _chat.TryEmoteWithChat(tripper, emote);

        var chasmEvent = new EntityStartedFallingIntoChasmEvent((tripper, falling));
        RaiseLocalEvent(chasm, ref chasmEvent);
        var tripperEvent = new StartedFallingIntoChasmEvent((chasm, chasm.Comp));
        RaiseLocalEvent(tripper, ref tripperEvent);

        Entity<ChasmFallingComponent> ret = (tripper, falling);
        Dirty(ret);
        return ret;
    }

    #endregion Public API
}
