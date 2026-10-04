using Content.Server.Actions;
using Content.Shared._AU14.Marines.Orders;
using Content.Shared._RMC14.Chat;
using Content.Shared._RMC14.Marines;
using Content.Shared.Chat;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._AU14.Marines.Orders;

public sealed partial class AU14SilenceOrderSystem : EntitySystem
{
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private SharedCMChatSystem _cmChat = default!;
    [Dependency] private EntityLookupSystem _entityLookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly HashSet<Entity<MarineComponent>> _receivers = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AU14SilenceOrderAbilityComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<AU14SilenceOrderAbilityComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<AU14SilenceOrderAbilityComponent, AU14SilenceActionEvent>(OnSilenceAction);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<AU14SilenceOrderComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.ExpiresAt < time)
                RemCompDeferred<AU14SilenceOrderComponent>(uid);
        }
    }

    private void OnStartup(Entity<AU14SilenceOrderAbilityComponent> ent, ref ComponentStartup args)
    {
        var comp = ent.Comp;
        _actions.AddAction(ent, ref comp.SilenceActionEntity, comp.SilenceAction);
        _actions.SetUseDelay(comp.SilenceActionEntity, comp.Cooldown);
    }

    private void OnShutdown(Entity<AU14SilenceOrderAbilityComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.SilenceActionEntity);
    }

    private void OnSilenceAction(Entity<AU14SilenceOrderAbilityComponent> ent, ref AU14SilenceActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp(ent, out TransformComponent? xform) || _mobState.IsDead(ent))
            return;

        args.Handled = true;

        _actions.StartUseDelay(ent.Comp.SilenceActionEntity);

        var expiresAt = _timing.CurTime + ent.Comp.Duration;

        _receivers.Clear();
        _entityLookup.GetEntitiesInRange(xform.Coordinates, ent.Comp.Range, _receivers);

        var noticeMsg = Loc.GetString("au14-silence-order-notice");

        foreach (var receiver in _receivers)
        {
            if (receiver.Owner == ent.Owner)
                continue;

            if (_mobState.IsDead(receiver))
                continue;

            var silence = EnsureComp<AU14SilenceOrderComponent>(receiver);
            silence.ExpiresAt = expiresAt;

            _popup.PopupEntity(noticeMsg, receiver, receiver, PopupType.Small);
            _cmChat.ChatMessageToOne(noticeMsg, receiver, ChatChannel.Local, colorOverride: new Color(0.75f, 0.75f, 1f, 1f));
        }

        SendSilenceEmote(ent.Owner);
    }

    private void SendSilenceEmote(EntityUid source)
    {
        var pronoun = Loc.GetString("au14-silence-order-emote-pronoun-other");
        if (TryComp<HumanoidAppearanceComponent>(source, out var appearance))
        {
            pronoun = appearance.Sex switch
            {
                Sex.Male => Loc.GetString("au14-silence-order-emote-pronoun-male"),
                Sex.Female => Loc.GetString("au14-silence-order-emote-pronoun-female"),
                _ => pronoun,
            };
        }

        var entityName = FormattedMessage.EscapeText(Name(Identity.Entity(source, EntityManager)));
        var emoteAction = Loc.GetString("au14-silence-order-emote-action", ("pronoun", pronoun));
        var wrappedMessage = $"[font size=14][bold][color=#C4A35A]{entityName} {emoteAction}[/color][/bold][/font]";

        _cmChat.ChatMessageToMany(
            emoteAction,
            wrappedMessage,
            Filter.Pvs(source),
            ChatChannel.Emotes,
            source
        );
    }
}
