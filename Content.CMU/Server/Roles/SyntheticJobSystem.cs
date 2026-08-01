using Content.Server.GameTicking.Events;
using Content.Server.Players.JobWhitelist;
using Content.Shared._CMU14.Roles;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Roles;

/// <summary>
/// Blocks players who don't hold the synthetic job whitelist from taking a synthetic
/// job via a path that doesn't go through GameTicker.Spawning.cs's character-based
/// resolution (e.g. ghost-role takeover of a synthetic body), since those paths don't
/// resolve a HumanoidCharacterProfile and so can't be checked by
/// AllegianceSystem.DoesCharacterMeetJobSynthetic. Normal round-start/late-join spawns
/// already enforce the stricter "character marked Synthetic" requirement separately;
/// this is a whitelist-only floor for jobs where no character is involved.
/// </summary>
public sealed class SyntheticJobSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly JobWhitelistManager _jobWhitelist = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IsJobAllowedEvent>(OnIsJobAllowed);
    }

    private void OnIsJobAllowed(ref IsJobAllowedEvent ev)
    {
        if (ev.Cancelled)
            return;

        if (!_prototypes.TryIndex(ev.JobId, out JobPrototype? job) || !job.IsSynthetic)
            return;

        if (!_jobWhitelist.IsAllowed(ev.Player, CMUSyntheticRoles.SyntheticWhitelistJob))
            ev.Cancelled = true;
    }
}
