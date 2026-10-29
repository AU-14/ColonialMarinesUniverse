using Content.Shared._RMC14.Chemistry.Effects;  
using Content.Shared._RMC14.Xenonids.Parasite;  
using Content.Shared.Damage.Systems;  
using Content.Shared.FixedPoint;  
using Robust.Shared.Network;  
using Robust.Shared.Prototypes;  
  
namespace Content.Shared.CMU14.Chemistry.Effects.Special;  
  
public sealed partial class Bloodbursting : RMCChemicalEffect  
{  
      
    [DataField]  
    public bool RequiresOverdose = true;  
  
    
    [DataField]  
    public EntProtoId EmbryoSpawn = "CMU14XenoBloodburster";  
  
      
    [DataField]  
    public TimeSpan? BurstDelay;  
  
    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)  
    {  
        if (RequiresOverdose)  
            return;  
  
        TryInfect(system, args);  
    }  
  
    protected override void TickOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)  
    {  
        TryInfect(system, args);  
    }  
  
    private void TryInfect(RMCChemicalEffectSystem system, RMCReagentEffectArgs args)  
    {  
        var entman = args.EntityManager;  
        var target = args.TargetEntity;  
  
          
        if (IoCManager.Resolve<INetManager>().IsClient)  
            return;  
  
          
        if (!entman.TryGetComponent<InfectableComponent>(target, out var infectable) ||  
            infectable.BeingInfected ||  
            entman.HasComponent<VictimInfectedComponent>(target))  
        {  
            return;  
        }  
  
        var parasiteSys = entman.System<SharedXenoParasiteSystem>();  
        var infected = entman.EnsureComp<VictimInfectedComponent>(target);  
  
        
        if (BurstDelay != null)  
            parasiteSys.SetBurstDelay((target, infected), BurstDelay.Value);  
  
        parasiteSys.SetBurstSpawn((target, infected), EmbryoSpawn);  
        parasiteSys.RefreshIncubationMultipliers((target, infected));  
        entman.Dirty(target, infected);  
    }  
  
    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)  
        => RequiresOverdose  
            ? "Overdoses implant a neomorph embyro."  
            : "Implants a neomorph embyro.";  
}
