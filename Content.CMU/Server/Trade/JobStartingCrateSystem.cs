// SPDX-License-Identifier: AGPL-3.0-only
using Content.Shared.CMU14.Trade;
using Content.Shared.GameTicking;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Trade;

// A character of a job that has a starting crate comes into the round with it beside them.
public sealed class JobStartingCrateSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (args.JobId is not { } job)
            return;

        foreach (var crate in _proto.EnumeratePrototypes<JobStartingCratePrototype>())
        {
            if (crate.Job == job)
                SpawnNextToOrDrop(crate.Crate, args.Mob);
        }
    }
}
