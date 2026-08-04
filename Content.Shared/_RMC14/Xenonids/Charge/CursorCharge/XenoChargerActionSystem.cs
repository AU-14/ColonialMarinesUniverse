using Content.Shared._RMC14.Xenonids.Charge.CursorCharge;
using Content.Shared._RMC14.Xenonids.ChargerLunge;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared._RMC14.Xenonids.Charge.CursorCharge;

public sealed partial class XenoChargerActionSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private XenoChargerMovementSystem _movement = default!;


    public override void Initialize()
    {
        SubscribeLocalEvent<XenoChargerComponent, XenoCursorChargingActionEvent>(OnToggleCharge);
        SubscribeLocalEvent<XenoChargerComponent, XenoChargerResetEvent>(OnChargerReset);
    }

    private void OnChargerReset(Entity<XenoChargerComponent> xeno, ref XenoChargerResetEvent args)
    {
        if (_net.IsClient)
            return;

        foreach (var action in _actions.GetActions(xeno))
        {
            if (_actions.GetEvent(action) is not XenoCursorChargingActionEvent)
                continue;

            var cooldown = args.Completed ? xeno.Comp.ChargeCooldown : xeno.Comp.EarlyEndCooldown;
            _actions.SetCooldown((action.Owner, (ActionComponent?) action.Comp), cooldown);
            break;
        }
    }
    private void OnToggleCharge(Entity<XenoChargerComponent> xeno, ref XenoCursorChargingActionEvent args)
    {
        if (args.Handled)
            return;

        if (_net.IsClient)
            return;

        args.Handled = true;

        TryComp(xeno.Owner, out XenoChargerStateComponent? stateComp);
        var moveState = stateComp?.MoveState ??  XenoChargerMoveState.Idle;


        switch (moveState)
        {
            case XenoChargerMoveState.Idle:
                _movement.StartCharge(xeno.Owner);

                if (_net.IsServer)
                    _popup.PopupEntity(Loc.GetString("rmc-xeno-charge-start", ("xeno", xeno.Owner)),
                        xeno, PopupType.Small);
                break;

            case XenoChargerMoveState.Charging:
                var isCharged = (stateComp?.Stage ?? 0) > 0;
                _movement.StartLunge(xeno.Owner);

                if (_net.IsServer)
                {
                    var msgKey = isCharged ? "rmc-xeno-lunge-charged-activate" : "rmc-xeno-lunge-activate";
                    _popup.PopupEntity(Loc.GetString(msgKey, ("xeno", xeno.Owner)), xeno, PopupType.Small);
                    _audio.PlayPvs(new SoundPathSpecifier("/Audio/_RMC14/Xeno/alien_pounce.ogg"), xeno);
                }
                break;

            case XenoChargerMoveState.Lunging:
                break;
        }
    }
}
