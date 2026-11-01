using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Shared.Chat;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Database;
using Content.Shared.Ghost.Components;
using Content.Shared.Mobs.Systems;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.Yautja;

public enum YautjaOverseerNetwork : byte
{
    All,
    HuntingParty,
    Stranded,
}

// cmss13 elder_overseer_message: big red text + overseer voice to every living pred on the picked net
public sealed partial class YautjaOverseerSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogs = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    private static readonly SoundSpecifier OverseerSound = new SoundCollectionSpecifier("CMUYautjaElderOverseer");

    private const string HuntingPartyFaction = "CMUYautja";
    private const string StrandedFaction = "CMUYautjaStranded";

    public static bool TryParseNetwork(string text, out YautjaOverseerNetwork network)
    {
        switch (text.ToLowerInvariant())
        {
            case "all":
                network = YautjaOverseerNetwork.All;
                return true;
            case "hunt":
            case "hunters":
            case "huntingparty":
                network = YautjaOverseerNetwork.HuntingParty;
                return true;
            case "stranded":
                network = YautjaOverseerNetwork.Stranded;
                return true;
            default:
                network = default;
                return false;
        }
    }

    public int SendOverseerMessage(string text, YautjaOverseerNetwork network, string author)
    {
        // staff verb never passes a title, so cmss13 names it by the net it went out on
        var title = Loc.GetString(network switch
        {
            YautjaOverseerNetwork.HuntingParty => "cmu-yautja-overseer-title-elder",
            YautjaOverseerNetwork.Stranded => "cmu-yautja-overseer-title-stranded",
            _ => "cmu-yautja-overseer-title-all",
        });

        var message = Loc.GetString("cmu-yautja-overseer-message",
            ("title", FormattedMessage.EscapeText(title)),
            ("text", FormattedMessage.EscapeText(text)));

        var filter = Filter.Empty().AddWhereAttachedEntity(e => IsRecipient(e, network));
        var count = filter.Count;

        _chat.ChatMessageToManyFiltered(filter, ChatChannel.Radio, message, message, default, false, true, null);
        _audio.PlayGlobal(OverseerSound, filter, true, AudioParams.Default.WithVolume(-4f));

        _adminLogs.Add(LogType.Chat, LogImpact.Medium,
            $"{author} sent a Yautja Elder Overseer message to {network}: {text}");
        _chat.SendAdminAlert(Loc.GetString("cmu-yautja-overseer-admin-alert",
            ("author", author),
            ("network", network.ToString())));

        return count;
    }

    private bool IsRecipient(EntityUid uid, YautjaOverseerNetwork network)
    {
        // ghosts read it too so observing staff can see what went out
        if (HasComp<GhostComponent>(uid))
            return true;

        if (!TryComp(uid, out YautjaComponent? yautja) || _mobState.IsDead(uid))
            return false;

        return network switch
        {
            YautjaOverseerNetwork.HuntingParty => yautja.NpcFaction == HuntingPartyFaction,
            YautjaOverseerNetwork.Stranded => yautja.NpcFaction == StrandedFaction,
            _ => true,
        };
    }
}
