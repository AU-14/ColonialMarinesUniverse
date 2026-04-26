using Content.Shared._RMC14.Chemistry;
using Content.Shared._RMC14.Xenonids.Acid;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Xenonids.Acid;

public sealed class AcidExtinguisherCleanupSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly SharedXenoAcidSystem _xenoAcid = default!;

    private static readonly ProtoId<ReagentPrototype> AcidRemovedBy = "Water";

    public override void Initialize()
    {
        SubscribeLocalEvent<CorrosiveAcidLinkComponent, VaporHitEvent>(OnAcidLinkVaporHit);
    }

    private void OnAcidLinkVaporHit(Entity<CorrosiveAcidLinkComponent> ent, ref VaporHitEvent args)
    {
        TryWashAcid(ent.Comp.Target, args);
    }

    private void TryWashAcid(EntityUid target, VaporHitEvent args)
    {
        if (!_xenoAcid.TryGetAcidStrength(target, out var strength))
            return;

        if (strength >= XenoAcidStrength.Strong)
            return;

        var solEnt = args.Solution;
        foreach (var (_, solution) in _solutionContainer.EnumerateSolutions((solEnt, solEnt)))
        {
            if (!solution.Comp.Solution.ContainsReagent(AcidRemovedBy, null))
                continue;

            _xenoAcid.RemoveAcid(target);
            return;
        }
    }
}
