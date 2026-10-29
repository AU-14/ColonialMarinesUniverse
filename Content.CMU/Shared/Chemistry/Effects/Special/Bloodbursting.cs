using Content.Shared._RMC14.Chemistry.Effects;  
using Content.Shared._RMC14.Xenonids.Parasite;  
using Content.Shared.Damage;  
using Content.Shared.EntityEffects;  
using Content.Shared.FixedPoint;  
using Robust.Shared.Network;  
using Robust.Shared.Prototypes;  
  
namespace Content.Shared._CMU14.Chemistry.Effects.Special;  
  
public sealed partial class Embryonic : RMCChemicalEffect  
{  
     
    [DataField]  
    public bool RequiresOverdose = true;  
  
     
    [DataField]  
    public EntProtoId? EmbryoSpawn = "CMU14XenoBloodburster";
  
     
    [DataField]  
    public TimeSpan? BurstDelay;  
  
    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)  
    {  
        if (RequiresOverdose)  
            return;  
  
        TryInfect(args);  
    }  
  
    protected override void TickOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)  
    {  
        TryInfect(args);  
    }  
  
    private void TryInfect(EntityEffectReagentArgs args)  
    {  
        var entman = args.EntityManager;  
        var targ = args.TargetEntity;  
  
        var net = IoCManager.Resolve<INetManager>();  
        if (!net.IsServer)  
            return;  
  
        
        if (!entman.TryGetComponent<InfectableComponent>(targ, out var infectable))  
            return;  
        if (infectable.BeingInfected)  
            return;  
        if (entman.HasComponent<VictimInfectedComponent>(targ))  
            return;
  
        var parasiteSys = entman.System<SharedXenoParasiteSystem>();  
        var infected = entman.EnsureComp<VictimInfectedComponent>(targ);  
  
         
        if (BurstDelay != null)  
            parasiteSys.SetBurstDelay((targ, infected), BurstDelay.Value);  
        if (EmbryoSpawn != null)  
            parasiteSys.SetBurstSpawn((targ, infected), EmbryoSpawn.Value);  
  
        parasiteSys.RefreshIncubationMultipliers((targ, infected));  
        entman.Dirty(targ, infected);  
    }  
  
    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)  
    {  
        return RequiresOverdose  
            ? "Overdoses implant a neomorph embryo."  
            : "Implants a neomorph embryo.";  
    }  
}
