using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.CMU14.Item.HoldUp;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory.VirtualItem;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Item.HoldUp;

public sealed class CMUHoldUpItemSystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeNetworkEvent<CMUHoldUpItemEvent>(OnHoldUpItem);
    }

    private void OnHoldUpItem(CMUHoldUpItemEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user)
            return;

        if (!_hands.TryGetHeldItem(user, msg.HandName, out var item) ||
            HasComp<VirtualItemComponent>(item))
        {
            return;
        }

        var cooldown = EnsureComp<CMUHoldUpItemCooldownComponent>(user);
        var now = _timing.CurTime;
        if (now < cooldown.NextHoldUpAt)
            return;

        cooldown.NextHoldUpAt = now + cooldown.Cooldown;

        // Passing the session also applies the regular chat rate limit.
        _chat.TrySendInGameICMessage(user,
            Loc.GetString("cmu-hold-up-item-emote", ("item", item.Value)),
            InGameICChatType.Emote,
            ChatTransmitRange.Normal,
            player: args.SenderSession);
    }
}
