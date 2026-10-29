using Content.Shared._RMC14.Xenonids;  
using Content.Shared._RMC14.Xenonids.Parasite;  
using Content.Shared.CMU14.Xenomorphs.Pathogen;  
using Content.Shared.CMU14.Xenomorphs.Pathogen.MycotoxinInject;  
using Content.Shared.CMU14.Xenomorphs.Pathogen.Walker;
using Content.Shared.Popups.SharedPopupSystem;
using Content.Shared.Mobs;  
using Content.Shared.Mobs.Components;  
using Robust.Shared.GameObjects;  
using Robust.Shared.Network;    
  
namespace Content.Shared.CMU14.Xenomorphs.Pathogen;  
  
public sealed partial class MycotoxConversionSystem : EntitySystem  
{  
    [Dependency] private INetManager _net = default!;  
    [Dependency] private SharedPopupSystem _popup = default!;  
  
    public override void Initialize()  
    {  
        SubscribeLocalEvent<MycotoxPendingComponent, MobStateChangedEvent>(OnMobStateChanged);  
    }  
  
    private void OnMobStateChanged(Entity<MycotoxPendingComponent> ent, ref MobStateChangedEvent args)  
    {  
        if (args.NewMobState != MobState.Dead)  
            return;  
  
        RemCompDeferred<MycotoxPendingComponent>(ent); // consume the marker regardless of outcome  
  
        if (_net.IsClient)  
            return;  
  
        var target = ent.Owner;  
  
        // same exclusions as CMUXenoMycotoxinInjectSystem.ValidateTarget  
        if (HasComp<XenoComponent>(target) ||  
            HasComp<VictimInfectedComponent>(target) ||  
            HasComp<CMUPathogenWalkerComponent>(target))  
        {  
            return;  
        }  
  
        _popup.PopupEntity(Loc.GetString("cmu14-mycotoxin-conversion-begin"), target, PopupType.LargeCaution);  
  
        RaiseLocalEvent(new CMUMycotoxinInjectDoReanimateEvent(target, target));  
    }  
}
