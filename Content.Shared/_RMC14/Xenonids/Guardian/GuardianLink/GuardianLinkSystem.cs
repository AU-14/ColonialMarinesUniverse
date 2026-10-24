using Content.Shared._RMC14.Xenonids.Guardian.XenoGuardianSystem.cs;
using Content.Shared._RMC14.Xenonids.Hive;  
using Content.Shared._RMC14.Xenonids.Plasma;  
using Content.Shared.Actions;  
using Content.Shared.Examine;  
using Content.Shared.Mobs.Systems;  
using Content.Shared.Popups;  
using Robust.Shared.Network;  
using Robust.Shared.Timing;  
  
namespace Content.Shared._RMC14.Xenonids.Guardian;  
  
public sealed partial class XenoGuardianSystem : EntitySystem  
{  
    [Dependency] private readonly ExamineSystemShared _examine = default!;  
    [Dependency] private readonly GuardianAbsorbSystem _guardianAbsorb = default!;  
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;  
    [Dependency] private readonly MobStateSystem _mobState = default!;  
    [Dependency] private readonly INetManager _net = default!;  
    [Dependency] private readonly SharedPopupSystem _popup = default!;  
    [Dependency] private readonly IGameTiming _timing = default!;  
    [Dependency] private readonly XenoPlasmaSystem _xenoPlasma = default!;  
  
    public override void Initialize()  
    {  
        SubscribeLocalEvent<XenoGuardianComponent, XenoGuardianLinkActionEvent>(OnGuardianLinkAction);  
    }  
  
    private void OnGuardianLinkAction(Entity<XenoGuardianComponent> xeno, ref XenoGuardianLinkActionEvent args)  
    {  
        if (args.Handled)  
            return;  
  
        var target = args.Target;  
  
        if (!HasComp<XenoComponent>(target))  
        {  
            var msg = Loc.GetString("rmc-xeno-not-same-hive");  
            _popup.PopupClient(msg, xeno, xeno, PopupType.SmallCaution);  
            return;  
        }  
  
        if (!_hive.FromSameHive(xeno.Owner, target))  
        {  
            var msg = Loc.GetString("rmc-xeno-not-same-hive");  
            _popup.PopupClient(msg, target, xeno, PopupType.SmallCaution);  
            return;  
        }  
  
        if (xeno.Owner == target)  
        {  
            var msg = Loc.GetString("rmc-xeno-guardian-link-self");  
            _popup.PopupClient(msg, target, xeno, PopupType.SmallCaution);  
            return;  
        }  
  
        if (_mobState.IsDead(target))  
        {  
            var msg = Loc.GetString("rmc-xeno-guardian-link-dead", ("target", target));  
            _popup.PopupClient(msg, target, xeno, PopupType.SmallCaution);  
            return;  
        }  
  
        if (!_examine.InRangeUnOccluded(xeno.Owner, target, xeno.Comp.Range))  
        {  
            var msg = Loc.GetString("rmc-xeno-guardian-link-see-fail");  
            _popup.PopupClient(msg, xeno, xeno, PopupType.SmallCaution);  
            return;  
        }  
  
        if (!_xenoPlasma.TryRemovePlasmaPopup(xeno.Owner, xeno.Comp.LinkCost))  
            return;  
  
        args.Handled = true;  
  
        _guardianAbsorb.SetAbsorb(target, xeno, GuardianAbsorbType.DirectLink, xeno.Comp.AbsorbPercent);  
  
        var linked = EnsureComp<GuardianLinkedComponent>(target);  
        linked.ExpiresAt = _timing.CurTime + xeno.Comp.LinkDuration;  
        Dirty(target, linked);  
  
        var selfMsg = Loc.GetString("rmc-xeno-guardian-link-start-self", ("target", target));  
        _popup.PopupClient(selfMsg, target, xeno);  
  
        var targetMsg = Loc.GetString("rmc-xeno-guardian-link-start-target", ("user", xeno));  
        _popup.PopupEntity(targetMsg, target, target);  
    }  
  
    public override void Update(float frameTime)  
    {  
        if (_net.IsClient)  
            return;  
  
        var time = _timing.CurTime;  
        var query = EntityQueryEnumerator<GuardianLinkedComponent>();  
        while (query.MoveNext(out var uid, out var linked))  
        {  
            if (time < linked.ExpiresAt)  
                continue;  
  
            RemCompDeferred<GuardianLinkedComponent>(uid);  
            _guardianAbsorb.RemoveAbsorb(uid, GuardianAbsorbType.DirectLink);  
        }  
    }  
}
