using Content.Server.CMU14.Camera.Bodycam;
using Content.Shared._RMC14.Roles;
using Content.Shared.Access.Systems;
using Content.Shared.CMU14.BadgeNumber;
using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.CMU14.BadgeNumber;

/// <summary>
/// Gives every CMB, NSPA and CWPS officer a badge number on their ID, unique for the round. That covers colony jobs
/// joining normally and third-party officers whose bodies are dressed as ghost roles.
/// </summary>
public sealed class CMUBadgeNumberAssignmentSystem : EntitySystem
{
    private static readonly HashSet<string> BadgedForces = new(StringComparer.OrdinalIgnoreCase) { "CMB", "NSPA", "CWPS" };

    [Dependency] private CMUCameraGearSystem _cameraGear = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;

    private readonly HashSet<int> _issued = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<OriginalRoleComponent, ComponentStartup>(OnOriginalRoleStartup);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _issued.Clear());
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (args.JobId != null)
            TryAssign(args.Mob, args.JobId);
    }

    // Ghost role bodies are given their role once they're dressed, without a spawn complete event.
    private void OnOriginalRoleStartup(Entity<OriginalRoleComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.Job is { } job)
            TryAssign(ent, job);
    }

    private void TryAssign(EntityUid mob, string jobId)
    {
        if (!_prototypes.TryIndex<JobPrototype>(jobId, out var job) ||
            job.RoundForce == null ||
            !BadgedForces.Contains(job.RoundForce) ||
            !_idCard.TryFindIdCard(mob, out var card) ||
            HasComp<CMUBadgeNumberComponent>(card))
        {
            return;
        }

        var badge = EnsureComp<CMUBadgeNumberComponent>(card);
        badge.Number = NextNumber();
        Dirty(card, badge);

        // Camera gear already on lists the officer by badge number.
        _cameraGear.RefreshWornBy(mob);
    }

    private int NextNumber()
    {
        // 9000 numbers; past that they repeat rather than spin forever.
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var number = _random.Next(1000, 10000);
            if (_issued.Add(number))
                return number;
        }

        for (var number = 1000; number < 10000; number++)
        {
            if (_issued.Add(number))
                return number;
        }

        return _random.Next(1000, 10000);
    }
}
