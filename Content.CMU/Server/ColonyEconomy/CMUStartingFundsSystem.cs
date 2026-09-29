using System.Linq;
using Content.Shared.Access.Systems;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.CMU14.ColonyEconomy;

/// <summary>
/// Credits every spawning character's ID card account with starting money based on their job's pay tier.
/// </summary>
public sealed class CMUStartingFundsSystem : EntitySystem
{
    [Dependency] private SharedIdCardSystem _idCard = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (args.JobId == null ||
            GetTier(args.JobId) is not { } tier ||
            !_idCard.TryFindIdCard(args.Mob, out var idCard))
        {
            return;
        }

        idCard.Comp.AccountBalance += _random.Next(tier.Min, tier.Max + 1);
        Dirty(idCard);
    }

    private CMUStartingFundsPrototype? GetTier(string jobId)
    {
        CMUStartingFundsPrototype? fallback = null;
        foreach (var tier in ProtoMan.EnumeratePrototypes<CMUStartingFundsPrototype>().OrderByDescending(t => t.Priority))
        {
            if (tier.Jobs.Contains(jobId))
                return tier;

            foreach (var departmentId in tier.Departments)
            {
                if (ProtoMan.TryIndex(departmentId, out var department) &&
                    department.Roles.Contains(jobId))
                {
                    return tier;
                }
            }

            if (tier.Default)
                fallback ??= tier;
        }

        return fallback;
    }
}
