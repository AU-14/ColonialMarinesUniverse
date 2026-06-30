using Content.Server.Actions;
using Content.Server.Chat.Systems;
using Content.Shared._AU14.Marines.Orders;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._AU14.Marines.Orders;

public sealed partial class AU14CallToAttentionSystem : EntitySystem
{
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private EntityLookupSystem _entityLookup = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly HashSet<Entity<HumanoidAppearanceComponent>> _receivers = new();

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
        _actions.StartUseDelay(ent.Comp.ActionEntity);

        SendCallout(ent);

        _receivers.Clear();
        _entityLookup.GetEntitiesInRange(xform.Coordinates, ent.Comp.Range, _receivers);

        var noticeMsg = Loc.GetString("au14-call-to-attention-notice");
        var emote = ent.Comp.Emote;
        var maxDelay = ent.Comp.ResponseStagger;
        var whisperExpiresAt = _timing.CurTime + ent.Comp.WhisperDuration;

        foreach (var receiver in _receivers)
        {
            if (_mobState.IsDead(receiver))
                continue;

            var target = receiver.Owner;
            if (!_examine.InRangeUnOccluded(ent.Owner, target, ent.Comp.Range, uid => uid == ent.Owner || uid == target))
                continue;

            ApplyWhisperEffect(target, whisperExpiresAt);

            var delay = maxDelay <= TimeSpan.Zero
                ? TimeSpan.Zero
                : TimeSpan.FromSeconds(_random.NextDouble() * maxDelay.TotalSeconds);

            Timer.Spawn(delay, () => SnapToAttention(target, emote, noticeMsg));
        }
    }

    private void SendCallout(Entity<AU14CallToAttentionAbilityComponent> ent)
    {
        if (ent.Comp.Callouts.Count == 0)
            return;

        var callout = _random.Pick(ent.Comp.Callouts);
        _chat.TrySendInGameICMessage(ent, Loc.GetString(callout), InGameICChatType.Speak, false, ignoreActionBlocker: true);
    }

    private void ApplyWhisperEffect(EntityUid target, TimeSpan expiresAt)
    {
        if (HasComp<AU14CallToAttentionWhisperImmuneComponent>(target))
            return;

        var silence = EnsureComp<AU14SilenceOrderComponent>(target);
        if (silence.ExpiresAt < expiresAt)
            silence.ExpiresAt = expiresAt;
    }

    private void SnapToAttention(EntityUid target, ProtoId<EmotePrototype> emote, string noticeMsg)
    {
        if (TerminatingOrDeleted(target) || _mobState.IsDead(target))
            return;

        _popup.PopupEntity(noticeMsg, target, target, PopupType.Small);
        _chat.TryEmoteWithChat(target, emote, ignoreActionBlocker: true, forceEmote: true);
    }
}
