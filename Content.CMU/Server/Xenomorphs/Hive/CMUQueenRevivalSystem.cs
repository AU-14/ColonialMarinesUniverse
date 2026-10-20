using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Mobs;

namespace Content.Server.CMU14.Xenomorphs.Hive;

public sealed partial class CMUQueenRevivalSystem : EntitySystem
{
    [Dependency] private SharedXenoHiveSystem _hive = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    // death clears the hive's CurrentQueen but nothing put it back when the queen came back to life
    // some other way than the reaper (rejuvenate, anything that heals a corpse with revives allowed).
    // hive collapse then ended the round on a hive whose queen was walking around
    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.OldMobState != MobState.Dead || args.NewMobState == MobState.Dead)
            return;

        if (!HasComp<XenoEvolutionGranterComponent>(args.Target) ||
            _hive.GetHive(args.Target) is not { } hive ||
            hive.Comp.CurrentQueen != null)
        {
            return;
        }

        _hive.SetHiveQueen(args.Target, hive);
    }
}
