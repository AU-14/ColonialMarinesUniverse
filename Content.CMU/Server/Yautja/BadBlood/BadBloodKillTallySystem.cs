using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Mobs;

namespace Content.Server.CMU14.Yautja.BadBlood;

// YautjaHonorWorthComponent.LifeKillsTotal also counts Yautja and synth kills, so team-killing would raise it.
// this one only counts kills that would earn Superiority Points
public sealed partial class BadBloodKillTallySystem : EntitySystem
{
    [Dependency] private BadBloodSystem _badBlood = default!;
    [Dependency] private BadBloodProvenanceSystem _provenance = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead ||
            args.OldMobState >= args.NewMobState ||
            args.Origin is not { } killer ||
            killer == args.Target ||
            TerminatingOrDeleted(killer) ||
            !_badBlood.IsBadBlood(killer) ||
            !_provenance.IsWorthyPrey(args.Target))
        {
            return;
        }

        EnsureComp<BadBloodKillTallyComponent>(killer).Kills++;
    }
}
