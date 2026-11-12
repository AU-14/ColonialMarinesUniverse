using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Mobs;
using Content.Shared.Traits.Assorted;

namespace Content.Server.CMU14.Yautja;

// a dead pred stays dead. this also opens the bracer overload verb straight away, it keys off Unrevivable
public sealed partial class YautjaPermadeathSystem : EntitySystem
{
    [Dependency] private RMCUnrevivableSystem _unrevivable = default!;

    public override void Initialize()
    {
        // broadcast, YautjaCloakSystem already owns the directed (YautjaComponent, MobStateChangedEvent) pair
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead ||
            !HasComp<YautjaComponent>(args.Target) ||
            HasComp<UnrevivableComponent>(args.Target))
        {
            return;
        }

        // killLarva off so an infected pred still bursts into an abomination
        if (HasComp<RMCRevivableComponent>(args.Target))
            _unrevivable.MakeUnrevivable(args.Target, killLarva: false);
        else
            EnsureComp<UnrevivableComponent>(args.Target);
    }
}
