using Content.Server.Actions;
using Content.Server.Chat.Systems;
using Content.Shared._AU14.Marines.Orders;
using Content.Shared._CMU14.Round.Roles;
using Content.Shared._RMC14.Roles;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._AU14.Marines.Orders;

public sealed partial class AU14CallToAttentionSystem : EntitySystem
{
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private SharedBuckleSystem _buckle = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private EntityLookupSystem _entityLookup = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private RotateToFaceSystem _rotateToFace = default!;
    [Dependency] private AU14SilenceOrderSystem _silence = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private readonly HashSet<Entity<HumanoidAppearanceComponent>> _receivers = new();
    private readonly List<EntityUid> _visibleTargets = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AU14CallToAttentionAbilityComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<AU14CallToAttentionAbilityComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<AU14CallToAttentionAbilityComponent, AU14CallToAttentionActionEvent>(OnCallToAttentionAction);
    }

    private void OnStartup(Entity<AU14CallToAttentionAbilityComponent> ent, ref ComponentStartup args)
    {
        var comp = ent.Comp;
        _actions.AddAction(ent, ref comp.ActionEntity, comp.Action);
        _actions.SetUseDelay(comp.ActionEntity, comp.Cooldown);
    }

    private void OnShutdown(Entity<AU14CallToAttentionAbilityComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);
    }

    private void OnCallToAttentionAction(Entity<AU14CallToAttentionAbilityComponent> ent, ref AU14CallToAttentionActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp(ent, out TransformComponent? xform) || _mobState.IsDead(ent))
            return;

        args.Handled = true;
        StartSharedCooldown();

        SendCallout(ent);

        _receivers.Clear();
        _entityLookup.GetEntitiesInRange(xform.Coordinates, ent.Comp.Range, _receivers);

        var noticeMsg = Loc.GetString("au14-call-to-attention-notice");
        var emote = ent.Comp.Emote;
        var maxDelay = ent.Comp.ResponseStagger;

        _visibleTargets.Clear();
        foreach (var receiver in _receivers)
        {
            if (receiver.Owner == ent.Owner)
                continue;

            if (_mobState.IsDead(receiver))
                continue;

            var target = receiver.Owner;
            if (!_examine.InRangeUnOccluded(ent.Owner, target, ent.Comp.Range, uid => uid == ent.Owner || uid == target))
                continue;

            _visibleTargets.Add(target);
        }

        _visibleTargets.RemoveAll(uid => !IsValidTarget(uid));

        var attentionFocus = GetAttentionFocus(ent.Owner, _visibleTargets);
        foreach (var target in _visibleTargets)
        {
            ApplyWhisperEffect(target, ent.Comp.WhisperDuration);

            var delay = maxDelay <= TimeSpan.Zero
                ? TimeSpan.Zero
                : TimeSpan.FromSeconds(_random.NextDouble() * maxDelay.TotalSeconds);

            Timer.Spawn(delay, () => SnapToAttention(target, attentionFocus, emote, noticeMsg));
        }
    }

    private void SendCallout(Entity<AU14CallToAttentionAbilityComponent> ent)
    {
        if (ent.Comp.Callouts.Count == 0)
            return;

        var callout = _random.Pick(ent.Comp.Callouts);
        _chat.TrySendInGameICMessage(ent, Loc.GetString(callout), InGameICChatType.Speak, false, ignoreActionBlocker: true);
    }

    private void StartSharedCooldown()
    {
        var query = EntityQueryEnumerator<AU14CallToAttentionAbilityComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            _actions.StartUseDelay(comp.ActionEntity);
        }
    }

    private void ApplyWhisperEffect(EntityUid target, TimeSpan duration)
        => _silence.ApplySilenceFor(target, duration);

    private void SnapToAttention(EntityUid target, EntityUid attentionFocus, ProtoId<EmotePrototype> emote, string noticeMsg)
    {
        if (TerminatingOrDeleted(target) || _mobState.IsDead(target))
            return;

        StandFromSeat(target);
        FaceAttentionFocus(target, attentionFocus);

        _popup.PopupEntity(noticeMsg, target, target, PopupType.Small);
        _chat.TryEmoteWithChat(target, emote, ignoreActionBlocker: true, forceEmote: true);
    }

    private void StandFromSeat(EntityUid target)
    {
        if (TryComp(target, out BuckleComponent? buckle) && buckle.Buckled)
            _buckle.TryUnbuckle(target, target, buckle, popup: false);
    }

    private EntityUid GetAttentionFocus(EntityUid caller, List<EntityUid> candidates)
    {
        var focus = caller;
        var highestPriority = 0;
        var shortestDistance = float.MaxValue;
        var callerCoords = _transform.GetMapCoordinates(caller);

        foreach (var candidate in candidates)
        {
            if (TerminatingOrDeleted(candidate) ||
                _mobState.IsDead(candidate) ||
                !TryComp(candidate, out AU14CallToAttentionAbilityComponent? ability) ||
                ability.AttentionFocusPriority <= 0)
            {
                continue;
            }

            var candidateCoords = _transform.GetMapCoordinates(candidate);
            if (candidateCoords.MapId != callerCoords.MapId)
                continue;

            var distance = (candidateCoords.Position - callerCoords.Position).LengthSquared();
            if (ability.AttentionFocusPriority < highestPriority ||
                ability.AttentionFocusPriority == highestPriority && distance >= shortestDistance)
            {
                continue;
            }

            focus = candidate;
            highestPriority = ability.AttentionFocusPriority;
            shortestDistance = distance;
        }

        return focus;
    }

    private void FaceAttentionFocus(EntityUid target, EntityUid attentionFocus)
    {
        if (target == attentionFocus || TerminatingOrDeleted(attentionFocus))
            return;

        var focusCoords = _transform.GetMapCoordinates(attentionFocus);
        var targetCoords = _transform.GetMapCoordinates(target);

        if (focusCoords.MapId != targetCoords.MapId)
            return;

        _rotateToFace.TryFaceCoordinates(target, focusCoords.Position);
    }

    private bool IsValidTarget(EntityUid target)
    {
        if (HasComp<AU14CallToAttentionImmuneComponent>(target))
            return false;

        return TryComp<OriginalRoleComponent>(target, out var orig)
            && orig.Job is { } jobId
            && _proto.TryIndex(jobId, out JobPrototype? proto)
            && (proto.RoundSide == RoundJobSide.Govfor || proto.RoundSide == RoundJobSide.Opfor);
    }
}
