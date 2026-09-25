using System.Linq;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared.CMU14.Lobby;
using Content.Shared.CMU14.Round.Roles;
using Content.Shared.Clothing;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Server.GameTicking;

public sealed partial class GameTicker
{
    private void OnLineupCharacterChanged(NetUserId userId)
    {
        if (RunLevel == GameRunLevel.PreRoundLobby &&
            _playerGameStatuses.GetValueOrDefault(userId) == PlayerGameStatus.ReadyToPlay)
            UpdateInfoText();
    }

    internal List<LobbyLineupEntry> GetLobbyLineup()
    {
        var entries = new List<LobbyLineupEntry>();
        if (RunLevel != GameRunLevel.PreRoundLobby || !LobbyPartySettings.IsEnabled(_cfg))
            return entries;

        var departments = _prototypeManager.EnumeratePrototypes<DepartmentPrototype>()
            .OrderByDescending(department => department.Weight).ThenBy(department => department.ID).ToArray();

        foreach (var session in _playerManager.NetworkedSessions)
        {
            if (session.Status != SessionStatus.InGame || !session.Channel.IsConnected ||
                _playerGameStatuses.GetValueOrDefault(session.UserId) != PlayerGameStatus.ReadyToPlay ||
                !_prefsManager.TryGetCachedPreferences(session.UserId, out var preferences))
                continue;

            var profile = preferences.SelectedCharacter;
            // This is a preference showcase, not a prediction of round-start job assignment.
            var job = profile.GetJobPrioritiesForGamemode((CurrentPreset ?? Preset)?.ID)
                .Where(pair => pair.Value != JobPriority.Never)
                .OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key.Id, StringComparer.Ordinal)
                .Select(pair => _prototypeManager.TryIndex(pair.Key, out var proto) ? proto : null)
                .FirstOrDefault(proto => proto is { Hidden: false } && proto.RoundSide != RoundJobSide.Threat);

            var (section, name, color, order) = GetLineupSection(profile, job, departments);
            string? loadoutKey = null;
            RoleLoadout? loadout = null;
            if (job != null)
            {
                var (key, prototype) = LoadoutSystem.GetJobLoadoutInfo(job.ID, _prototypeManager);
                if (prototype != null)
                {
                    loadoutKey = key;
                    // Resolve defaults for the owner's session, and avoid mutating the saved profile.
                    loadout = profile.Clone().GetLoadoutOrDefault(key, session, profile.Species,
                        EntityManager, _prototypeManager);
                }
            }

            entries.Add(new LobbyLineupEntry(session.UserId, profile, job?.ID, loadoutKey, loadout,
                section, name, color, order, _chatManager.EnsurePlayer(session.UserId).Key));
        }

        return entries.OrderBy(entry => entry.Order).ThenBy(entry => entry.SectionId, StringComparer.Ordinal)
            .ThenBy(entry => entry.Name, StringComparer.Ordinal).ThenBy(entry => entry.UserId.ToString()).ToList();
    }

    private (string Id, string Name, Color Color, int Order) GetLineupSection(
        HumanoidCharacterProfile profile, JobPrototype? job, DepartmentPrototype[] departments)
    {
        var department = job == null ? null : departments.FirstOrDefault(dep => dep.Roles.Contains(job.ID));
        var force = job?.RoundForce ?? string.Empty;
        var color = department?.Color ?? Color.FromHex("#9EB9C8");
        var prefix = string.IsNullOrEmpty(force) ? string.Empty : force + " / ";

        if (job?.RoundRole is "PlatoonCommander" or "ExecutiveOfficer" or "JuniorOfficer" or
            "Advisor" or "AdjutantDress" or "BrigadierGeneral" or "SectionSergeant")
            return (force + "/command", prefix + Loc.GetString("cmu-lobby-lineup-command"), color, 0);

        if (job != null && (job.HasSquad || job.RoundRole is "SquadSergeant" or "SquadRifleman" or
            "SquadAutomaticRifleman" or "SquadCombatTech" or "PlatoonCorpsman" or
            "RadioTelephoneOperator" or "WeaponsSpecialist" or "DroneOperator"))
        {
            if (_prototypeManager.TryIndex(profile.SquadPreference, out var squad) &&
                squad.TryGetComponent<SquadTeamComponent>(out var team, Factory) &&
                (string.IsNullOrEmpty(force) || string.Equals(team.Group, force, StringComparison.OrdinalIgnoreCase)))
                return (force + "/" + squad.ID, prefix + squad.Name, team.Color, 10);

            return (force + "/squads", prefix + Loc.GetString("cmu-lobby-lineup-squads"), color, 10);
        }

        if (job?.RoundSide is RoundJobSide.Govfor or RoundJobSide.Opfor)
            return (force + "/support", prefix + Loc.GetString("cmu-lobby-lineup-support"), color, 20);

        if (department != null)
            return (department.ID, Loc.GetString(department.Name), color, 30);

        return ("ready", Loc.GetString("cmu-lobby-lineup-unassigned"), color, 40);
    }
}
