using Content.Server.Chat.Managers;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Areas;
using Content.Shared.Chat;
using Content.Shared.Ghost;
using Robust.Shared.Player;

namespace Content.Server._CMU14.Yautja;

public sealed class YautjaSelfDestructNotificationSystem : EntitySystem
{
    [Dependency] private AreaSystem _area = default!;
    [Dependency] private IChatManager _chat = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaBracerComponent, YautjaSelfDestructArmedEvent>(OnSelfDestructArmed);
    }

    private void OnSelfDestructArmed(Entity<YautjaBracerComponent> ent, ref YautjaSelfDestructArmedEvent args)
    {
        var area = _area.GetAreaName(args.Hunter);
        var alert = args.Remote
            ? Loc.GetString(
                "cmu-yautja-self-destruct-admin-alert-remote",
                ("hunter", ToPrettyString(args.Hunter)),
                ("victim", ToPrettyString(args.Victim)),
                ("area", area))
            : Loc.GetString(
                "cmu-yautja-self-destruct-admin-alert-self",
                ("hunter", ToPrettyString(args.Hunter)),
                ("area", area));

        _chat.SendAdminAnnouncement(alert);
        _chat.SendAdminAnnouncement(Loc.GetString(
            "cmu-yautja-self-destruct-admin-cancel-link",
            ("bracer", ToPrettyString(args.Bracer)),
            ("victim", ToPrettyString(args.Victim))));

        var ghostMessage = Loc.GetString(
            "cmu-yautja-self-destruct-ghost-notify",
            ("victim", Name(args.Victim)));
        var ghostFilter = Filter.Empty().AddWhereAttachedEntity(HasComp<GhostComponent>);
        _chat.ChatMessageToManyFiltered(
            ghostFilter,
            ChatChannel.Notifications,
            ghostMessage,
            ghostMessage,
            EntityUid.Invalid,
            false,
            true,
            null);
    }
}
