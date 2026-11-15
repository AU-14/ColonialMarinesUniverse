using Content.Shared._RMC14.Chemistry.Effects;  
using Content.Shared._RMC14.Xenonids;  
using Content.Shared._RMC14.Xenonids.Parasite;  
using Content.Shared.CMU14.Xenomorphs.Pathogen;  
using Content.Shared.CMU14.Xenomorphs.Pathogen.Walker;  
using Content.Shared.Damage.Systems;  
using Content.Shared.FixedPoint;  
using Robust.Shared.Network;  
using Robust.Shared.Prototypes;  
using Robust.Shared.GameObjects;  
  
namespace Content.Shared.CMU14.Chemistry.Effects.Special;  
  
public sealed partial class Mycotoxigenic : RMCChemicalEffect  
{
    [DataField]  
    public bool RequiresOverdose = true;  
  
    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)  
    {  
        if (RequiresOverdose)  
            return;  
  
        TrySeed(system, args);  
    }  
  
    protected override void TickOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)  
    {  
        TrySeed(system, args);  
    }  
  
    private void TrySeed(RMCChemicalEffectSystem system, RMCReagentEffectArgs args)  
    {  
        var entman = IoCManager.Resolve<IEntityManager>();  
        var target = args.TargetEntity;  
  
        if (IoCManager.Resolve<INetManager>().IsClient)  
            return;  
  
        // Don't seed things that can never convert  
        if (entman.HasComponent<XenoComponent>(target) ||  
            entman.HasComponent<CMUPathogenWalkerComponent>(target) ||  
            entman.HasComponent<VictimInfectedComponent>(target) ||  
            entman.HasComponent<MycotoxPendingComponent>(target))  
        {  
            return;  
        }  
  
        entman.AddComponent<MycotoxPendingComponent>(target);  
    }  
  
    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)  
        => RequiresOverdose  
            ? "Overdoses seed dormant spores. The victim reanimates on death."  
            : "Seeds dormant spores. The victim reanimates on death.";  
}
