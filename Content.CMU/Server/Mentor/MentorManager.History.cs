using Content.Shared._RMC14.Mentor;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

// ReSharper disable once CheckNamespace
namespace Content.Server._RMC14.Mentor;

public sealed partial class MentorManager
{
    private const int MentorHistoryLimit = 200;
    private readonly Dictionary<NetUserId, List<MentorMessage>> _mentorHistory = new();

    private void RecordMentorHistory(MentorMessage message)
    {
        var history = _mentorHistory.GetOrNew(message.Destination);
        history.Add(message);
        if (history.Count > MentorHistoryLimit)
            history.RemoveAt(0);
    }

    private void SendMentorHistory(ICommonSession mentor)
    {
        _net.ServerSendMessage(new MentorMessagesReceivedMsg { IsHistory = true }, mentor.Channel);
        foreach (var history in _mentorHistory.Values)
        {
            foreach (var message in history)
            {
                _net.ServerSendMessage(new MentorMessagesReceivedMsg
                {
                    IsHistory = true,
                    Messages = new List<MentorMessage> { message },
                }, mentor.Channel);
            }
        }

        foreach (var (destination, claims) in _destinationClaims)
        {
            foreach (var claim in claims)
            {
                if (!_player.TryGetPlayerData(claim, out var player))
                    continue;

                _net.ServerSendMessage(new MentorClaimMsg
                {
                    Author = player.UserName,
                    Destination = destination,
                }, mentor.Channel);
            }
        }
    }
}
