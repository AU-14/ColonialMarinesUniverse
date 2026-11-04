using Content.Shared.CCVar;
using Content.Shared.CMU14.VoiceChat;
using Content.Shared._RMC14.Megaphone;
using Content.Shared._RMC14.Telephone;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.CMU14.VoiceChat;

/// <summary>
/// Voice over telephone calls, and megaphones carrying it further.
/// </summary>
public sealed partial class CMUVoiceChatSystem
{
    [Dependency] private SharedRMCTelephoneSystem _telephone = default!;

    private readonly List<INetChannel> _telephoneAudience = new(1);
    private bool _telephoneEnabled;
    private float _megaphoneRange;

    private void InitializeTelephone()
    {
        Subs.CVar(_cfg, CCVars.CMUVoiceChatTelephoneEnabled, value => _telephoneEnabled = value, true);
        Subs.CVar(_cfg, CCVars.CMUVoiceChatMegaphoneRange, value => _megaphoneRange = value, true);
    }

    /// <summary>
    /// A megaphone in the active hand boosts proximity voice and gives it the megaphone sound.
    /// </summary>
    public bool IsUsingMegaphone(EntityUid uid)
    {
        return _hands.GetActiveItem(uid) is { } item && HasComp<RMCMegaphoneComponent>(item);
    }

    /// <summary>
    /// The player holding the other handset of a connected call the speaker is on, if they can hear voice.
    /// </summary>
    public bool TryGetTelephoneListener(EntityUid speaker, out ICommonSession listener)
    {
        listener = default!;
        if (!_telephoneEnabled ||
            !_telephone.CMUTryGetCallListener(speaker, out var other) ||
            !_player.TryGetSessionByEntity(other, out var session) ||
            !_voice.Receives(session.UserId) ||
            !CanHear(other))
        {
            return false;
        }

        listener = session;
        return true;
    }

    private void SendTelephoneFrame(Speaker speaker, EntityUid uid, CMUVoiceWebFrame frame, NetEntity source, ICommonSession listener)
    {
        _telephoneAudience.Clear();
        _telephoneAudience.Add(listener.Channel);
        SendSpeakerInfo(speaker, uid, _telephoneAudience, string.Empty);
        _net.ServerSendMessage(new CMUMsgVoiceFrame
        {
            Source = source,
            Speaker = speaker.Id,
            Sequence = frame.Sequence,
            Flags = frame.Flags,
            Payload = frame.Payload,
            Route = CMUVoiceRoute.Telephone,
        }, listener.Channel);
    }
}
