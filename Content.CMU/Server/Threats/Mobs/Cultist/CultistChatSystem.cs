using Content.Shared._RMC14.Chat;
using Content.Shared.Chat;
using Content.Shared.Radio;
using CultistComponent = Content.Shared._CMU14.Threats.Mobs.Cultist.CultistComponent;

namespace Content.Server._CMU14.Threats.Mobs.Cultist;

public sealed class CultistChatSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CultistComponent, GetDefaultRadioChannelEvent>(OnCultistGetDefaultRadioChannel);
        SubscribeLocalEvent<CultistComponent, ChatGetPrefixEvent>(OnCultistGetPrefix);
    }

    private void OnCultistGetDefaultRadioChannel(Entity<CultistComponent> ent, ref GetDefaultRadioChannelEvent args)
    {
        args.Channel = SharedChatSystem.HivemindChannel;
    }

    private void OnCultistGetPrefix(Entity<CultistComponent> ent, ref ChatGetPrefixEvent args)
    {
        // Only allow Hivemind channel for cultists, mirror Xeno behavior
        if (args.Channel?.ID != SharedChatSystem.HivemindChannel.Id)
            args.Channel = null;
    }
}
