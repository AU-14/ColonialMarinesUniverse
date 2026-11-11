using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Tracker;
using Content.Shared._RMC14.Tracker.SquadLeader;
using Content.Shared.Alert;
using Content.Shared.CMU14.Squads;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Squads;

/// <summary>
/// Gives GOVFOR and OPFOR squad members a second tracker that always points at their squad leader,
/// shown above the normal squad tracker. The normal tracker then follows the member's fireteam
/// leader by default and can still be changed from the fireteam menu.
/// </summary>
public sealed partial class CMUSquadLeaderTrackerSystem : EntitySystem
{
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SquadSystem _squad = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private TrackerSystem _tracker = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly ProtoId<AlertCategoryPrototype> Category = "CMUSquadLeaderTracker";
    private const string AlertPrefix = "CMUSquadLeaderTracker";
    private static readonly HashSet<string> Groups = new(StringComparer.OrdinalIgnoreCase) { "GOVFOR", "OPFOR" };
    private static readonly TimeSpan UpdateEvery = TimeSpan.FromSeconds(1);

    private TimeSpan _nextUpdate;
    private HashSet<EntityUid> _shown = new();
    private HashSet<EntityUid> _stillShown = new();
    private readonly Dictionary<EntityUid, (string Alert, MapCoordinates? Leader)> _squadUpdates = new();

    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;
        if (time < _nextUpdate)
            return;

        _nextUpdate = time + UpdateEvery;
        _squadUpdates.Clear();

        var query = EntityQueryEnumerator<SquadLeaderTrackerComponent, SquadMemberComponent>();
        while (query.MoveNext(out var uid, out _, out var member))
        {
            if (member.Squad is not { } squad ||
                !TryComp(squad, out SquadTeamComponent? team) ||
                !Groups.Contains(team.Group) ||
                HasComp<CMUAuxiliarySquadComponent>(squad) ||
                HasComp<SquadLeaderComponent>(uid))
            {
                continue;
            }

            // The squad's leader search and world position are shared by all its members this update.
            if (!_squadUpdates.TryGetValue(squad, out var update))
            {
                var alert = AlertPrefix + Name(squad);
                if (!_prototypes.HasIndex<AlertPrototype>(alert))
                    alert = AlertPrefix;

                MapCoordinates? position = _squad.TryGetSquadLeader((squad, team), out var leader)
                    ? _transform.GetMapCoordinates(leader)
                    : null;
                update = (alert, position);
                _squadUpdates.Add(squad, update);
            }

            // With no squad leader the arrow shows as off.
            short severity = 0;
            if (update.Leader is { } leaderPosition)
                severity = _tracker.GetAlertSeverity(uid, leaderPosition);

            _alerts.ShowAlert(uid, update.Alert, severity);
            _stillShown.Add(uid);
        }

        // Clear the tracker from anyone who no longer qualifies, e.g. they took off their headset,
        // left the squad or became the squad leader.
        foreach (var uid in _shown)
        {
            if (!_stillShown.Contains(uid) && !TerminatingOrDeleted(uid))
                _alerts.ClearAlertCategory(uid, Category);
        }

        (_shown, _stillShown) = (_stillShown, _shown);
        _stillShown.Clear();
    }
}
