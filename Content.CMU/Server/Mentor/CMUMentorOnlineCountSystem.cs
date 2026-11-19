using Content.Server._RMC14.Mentor;
using Content.Server.Administration.Managers;
using Content.Shared.CMU14.Mentor;
using System.Linq;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;

namespace Content.Server.CMU14.Mentor;

/// <summary>
/// Answers the mhelp window's mentor-count request: how many mentors are connected and
/// receiving mentor helps right now. Stealthed admins are excluded, matching showmentors.
/// </summary>
public sealed class CMUMentorOnlineCountSystem : EntitySystem
{
    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private MentorManager _mentorManager = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        _net.RegisterNetMessage<CMUMentorOnlineCountRequestMsg>(OnCountRequested);
        _net.RegisterNetMessage<CMUMentorOnlineCountMsg>();
    }

    private void OnCountRequested(CMUMentorOnlineCountRequestMsg msg)
    {
        var count = _mentorManager
            .GetActiveMentors()
            .Count(m => _adminManager.GetAdminData(m, includeDeAdmin: true) is not { Stealth: true });

        _net.ServerSendMessage(new CMUMentorOnlineCountMsg { Count = count }, msg.MsgChannel);
    }
}
