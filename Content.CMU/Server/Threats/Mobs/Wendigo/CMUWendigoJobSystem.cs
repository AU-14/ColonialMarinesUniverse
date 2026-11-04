using Content.Server.Mind;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared.CMU14.Threats.Mobs.Wendigo;
using Content.Shared.Mind.Components;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Robust.Server.Player;

namespace Content.Server.CMU14.Threats.Mobs.Wendigo;

/// <summary>
/// Sets the Wendigo job on the mind of a player who takes a Wendigo with <see cref="CMUWendigoJobComponent"/>.
/// The threat spawn and ghost roles give the mind their own job right after the mind is added, so the Wendigo job
/// is applied on the next update, the way XenoRoleSystem applies a xeno's caste job.
/// </summary>
public sealed partial class CMUWendigoJobSystem : EntitySystem
{
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private PlayTimeTrackingSystem _playTime = default!;
    [Dependency] private SharedRoleSystem _roles = default!;

    private readonly HashSet<EntityUid> _pending = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUWendigoJobComponent, MindAddedMessage>(OnMindAdded);
    }

    private void OnMindAdded(Entity<CMUWendigoJobComponent> ent, ref MindAddedMessage args)
    {
        _pending.Add(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pending.Count == 0)
            return;

        foreach (var uid in _pending)
        {
            ApplyJob(uid);
        }

        _pending.Clear();
    }

    /// <summary>
    /// Gives the mind in <paramref name="uid"/> its Wendigo job. Returns false when there is no mind to update.
    /// </summary>
    public bool ApplyJob(EntityUid uid)
    {
        if (!TryComp(uid, out CMUWendigoJobComponent? wendigo) ||
            !_mind.TryGetMind(uid, out var mindId, out var mind))
        {
            return false;
        }

        if (_roles.MindHasRole<JobRoleComponent>((mindId, mind), out var job) &&
            job.Value.Comp1.JobPrototype == wendigo.Job)
        {
            return true;
        }

        _roles.MindAddJobRole(mindId, mind, silent: true, jobPrototype: wendigo.Job);
        if (mind.UserId is { } userId && _players.TryGetSessionById(userId, out var session))
            _playTime.PlayerRolesChanged(session);

        return true;
    }
}
