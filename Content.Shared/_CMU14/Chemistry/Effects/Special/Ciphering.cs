/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Atmos.Components;
using Content.Shared.Damage;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using System.Net.Http.Headers;

namespace Content.Shared._CMU14.Chemistry.Effects.Special;

public sealed partial class Ciphering : RMCChemicalEffect
{
    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return $"Does not have any known effects.\n" +
               $"Does not have any known overdose effects.\n"; //fancy schmancy way of saying it doesn't have one
    }
    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        base.Tick(damageable, potency, args);
        var entman = args.EntityManager;
        var netman = IoCManager.Resolve<INetManager>();
        var parasys = entman.System<SharedXenoParasiteSystem>();
        var targ = args.TargetEntity;
        if (netman.IsClient)
        {
            return;
        }
        Dictionary<string, EntityUid> hives = [];
        var hq = entman.EntityQueryEnumerator<HiveComponent>();
        while(hq.MoveNext(out var ent, out var comp))
        {
            if (entman.TryGetComponent<MetaDataComponent>(ent, out var met) && met.EntityPrototype is not null)
            {
                if (met.EntityPrototype.ID == "CMXenoHive")
                {
                    if (hives.ContainsKey("prime"))
                        continue;
                    hives.Add("prime", ent);
                }
                if (met.EntityPrototype.ID == "CMUCorruptedHive")
                {
                    if (hives.ContainsKey("corrupted"))
                        continue;
                    hives.Add("corrupted", ent);
                }
                if (met.EntityPrototype.ID == "CMUAlphaHive")
                {
                    if (hives.ContainsKey("alpha"))
                        continue;
                    hives.Add("alpha", ent);
                }
                if (met.EntityPrototype.ID == "CMUBravoHive")
                {
                    if (hives.ContainsKey("bravo"))
                        continue;
                    hives.Add("bravo", ent);
                }
                if (met.EntityPrototype.ID == "CMUCharlieHive")
                {
                    if (hives.ContainsKey("charlie"))
                        continue;
                    hives.Add("charlie", ent);
                }
                if (met.EntityPrototype.ID == "CMUDeltaHive")
                {
                    if (hives.ContainsKey("delta"))
                        continue;
                    hives.Add("delta", ent);
                }
            }
        }
        if (entman.TryGetComponent<VictimInfectedComponent>(targ, out var inf))
        {

            switch ((int)MathF.Round(Potency))
            {
                case 2:
                    var ghive = EntityUid.Invalid;
                    if (!hives.ContainsKey("corrupted"))
                    {
                        ghive = entman.Spawn("CMUCorruptedHive");
                    }
                    else ghive = hives["corrupted"];
                    parasys.SetHive(targ, ghive);
                    break;
                case 3:
                    var ahive = EntityUid.Invalid;
                    if (!hives.ContainsKey("alpha"))
                    {
                        ahive = entman.Spawn("CMUAlphaHive");
                    }
                    else ahive = hives["alpha"];
                    parasys.SetHive(targ, ahive);
                    break;
                case 4:
                    var bhive = EntityUid.Invalid;
                    if (!hives.ContainsKey("bravo"))
                    {
                        bhive = entman.Spawn("CMUBravoHive");
                    }
                    else bhive = hives["bravo"];
                    parasys.SetHive(targ, bhive);
                    break;
                case 5:
                    var chive = EntityUid.Invalid;
                    if (!hives.ContainsKey("charlie"))
                    {
                        chive = entman.Spawn("CMUCharlieHive");
                    }
                    else chive = hives["charlie"];
                    parasys.SetHive(targ, chive);
                    break;
                case 6:
                    var dhive = EntityUid.Invalid;
                    if (!hives.ContainsKey("delta"))
                    {
                        dhive = entman.Spawn("CMUDeltaHive");
                    }
                    else dhive = hives["delta"];
                    parasys.SetHive(targ, dhive);
                    break;
                default:
                    parasys.SetHive(targ, hives["prime"]);
                    break;
            }
        }
    }
}
