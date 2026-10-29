using Content.Shared._RMC14.Chemistry.Effects;  
using Content.Shared.CMU14.Xenomorphs.Pathogen.MycotoxinInject;  
using Content.Shared.CMU14.Xenomorphs.Pathogen.Walker;  
using Content.Shared._RMC14.Xenonids;  
using Content.Shared._RMC14.Xenonids.Parasite;  
using Content.Shared.Damage.Systems;  
using Content.Shared.FixedPoint;  
using Robust.Shared.Network;  
using Robust.Shared.Prototypes;  
  
namespace Content.Shared.CMU.Chemistry.Effects.Special;  
  
public sealed partial class Mycotoxigenic : RMCChemicalEffect  
{  
     
    [DataField]  
    public bool CanConvertLiving;  
  
    protected override bool ProcessOnDead => true;  
  
    protected override void TickOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)  
    {  
        if (IoCManager.Resolve<INetManager>().IsClient)  
            return;  
  
        var entman = IoCManager.Resolve<IEntityManager>();
        var target = args.TargetEntity;  
  
          
        var isDead = system.TryGetMobState(target, out var mobState) &&  
                     mobState.CurrentState == Mobs.MobState.Dead;  
        if (!isDead && !CanConvertLiving)  
            return;  
  
        if (entman.HasComponent<XenoComponent>(target))  
            return;  
        if (entman.HasComponent<VictimInfectedComponent>(target) ||  
            entman.HasComponent<CMUPathogenWalkerComponent>(target))  
            return;  
  
        
        entman.EventBus.RaiseLocalEvent(target, new CMUMycotoxinInjectDoReanimateEvent(target, target));  
    }  
  
    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)  
        => "Overdoses reanimate corpses as mycelium walkers.";  
}
