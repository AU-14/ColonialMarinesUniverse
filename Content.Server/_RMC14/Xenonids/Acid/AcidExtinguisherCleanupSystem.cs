using Content.Shared._RMC14.Chemistry;
using Content.Shared._RMC14.Xenonids.Acid;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Xenonids.Acid;

public sealed partial class AcidExtinguisherCleanupSystem : EntitySystem
{
    [Dependency] private SharedXenoAcidSystem _xenoAcid = default!;

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

        if (!args.Solution.Comp.Solution.ContainsReagent(AcidRemovedBy, null))
            return;

        _xenoAcid.RemoveAcid(target);
    }
}
