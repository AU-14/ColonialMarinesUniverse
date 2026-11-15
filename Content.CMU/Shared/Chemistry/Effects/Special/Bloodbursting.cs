using Content.Shared._RMC14.Chemistry.Effects;  
using Content.Shared._RMC14.Xenonids.Hive;  
using Content.Shared._RMC14.Xenonids.Parasite;  
using Content.Shared.Damage.Systems;  
using Content.Shared.FixedPoint;  
using Robust.Shared.Network;  
using Robust.Shared.Prototypes;  
using Robust.Shared.GameObjects;  
  
namespace Content.Shared.CMU.Chemistry.Effects.Special;  
  
public sealed partial class Bloodbursting : RMCChemicalEffect  
{  
  
    private const string PathogenHive = "CMUPathogenHive";  
  
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
        var entman = IoCManager.Resolve<IEntityManager>();  
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
        var infected = entman.AddComponent<VictimInfectedComponent>(target);  
  
  
        if (BurstDelay != null)  
            parasiteSys.SetBurstDelay((target, infected), BurstDelay.Value);  
  
        parasiteSys.SetBurstSpawn((target, infected), EmbryoSpawn);  
        // no hive meant the bloodburster came out hiveless, and the larva queue only hands out bodies of its own
        // hive, so it sat catatonic with players waiting. join the pathogen hive like spore infections do
        if (FindPathogenHive(entman) is { } hive)  
            parasiteSys.SetHive((target, infected), hive);  
        parasiteSys.RefreshIncubationMultipliers((target, infected));  
        entman.Dirty(target, infected);  
    }  
  
    private static EntityUid? FindPathogenHive(IEntityManager entman)  
    {  
        var query = entman.EntityQueryEnumerator<HiveComponent, MetaDataComponent>();  
        while (query.MoveNext(out var uid, out _, out var meta))  
        {  
            if (meta.EntityPrototype?.ID == PathogenHive)  
                return uid;  
        }  
  
        return null;  
    }  
  
    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)  
        => RequiresOverdose  
            ? "Overdoses implant a neomorph embyro."  
            : "Implants a neomorph embyro.";  
}
