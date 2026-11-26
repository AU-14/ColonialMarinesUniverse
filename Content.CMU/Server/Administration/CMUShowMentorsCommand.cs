using System.Linq;
using System.Text;
using Content.Server.Administration.Managers;
using Content.Server.Afk;
using Content.Server._RMC14.Mentor;
using Content.Shared.Administration;
using Content.Shared.Ghost.Components;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Administration;

/// <summary>
/// Player-facing counterpart to mentorwho: lists every online mentor with their admin rank, so
/// players know a mentor help is worth sending and who is staff. Admins count as mentors through
/// their MentorHelp flag; mentors without an admin rank show as plain Mentor. Stealthed admins
/// stay hidden, matching showadmins. Dementored mentors are listed with a marker since they do
/// not receive mentor helps.
/// </summary>
[AnyCommand]
public sealed partial class CMUShowMentorsCommand : LocalizedCommands
{
    [Dependency] private IAfkManager _afkManager = default!;
    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private MentorManager _mentorManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    public override string Command => "showmentors";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var active = new HashSet<ICommonSession>(_mentorManager.GetActiveMentors());
        var sb = new StringBuilder();
        var first = true;
        foreach (var mentor in _playerManager.Sessions
                     .Where(m => _mentorManager.IsMentor(m.UserId))
                     // Stealthed admins stay hidden, matching showadmins.
                     .Where(m => _adminManager.GetAdminData(m, includeDeAdmin: true) is not { Stealth: true })
                     .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(m => m.Name, StringComparer.Ordinal))
        {
            if (!first)
                sb.Append('\n');
            first = false;

            sb.Append(mentor.Name);

            var title = _adminManager.GetAdminData(mentor, includeDeAdmin: true)?.Title;
            if (string.IsNullOrEmpty(title))
                title = Loc.GetString("cmu-showmentors-mentor");
            sb.Append($": [{title}]");

            var isActive = active.Contains(mentor);
            if (!isActive)
            {
                sb.Append(' ').Append(Loc.GetString("cmu-showmentors-dementored"));
                continue;
            }

            // IC and AFK only mean something for mentors who can actually receive a mentor help.
            if (mentor.AttachedEntity is { } attached
                && !_entityManager.HasComponent<GhostComponent>(attached))
                sb.Append(' ').Append(Loc.GetString("cmu-showmentors-ic"));

            if (_afkManager.IsAfk(mentor))
                sb.Append(' ').Append(Loc.GetString("cmu-showmentors-afk"));
        }

        shell.WriteLine(first ? Loc.GetString("cmu-showmentors-none") : sb.ToString());
    }
}
