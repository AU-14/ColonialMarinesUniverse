using Content.Shared._RMC14.Stamina;
using Content.Shared._RMC14.Standing;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Tackle;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.ShakeStun;

public sealed partial class StunShakeableSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLogs = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RMCStandingSystem _rmcStanding = default!;
    [Dependency] private StatusEffectQuerySystem _statusEffects = default!;
    [Dependency] private RMCSizeStunSystem _sizeStun = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly ProtoId<StatusEffectPrototype> Unconscious = "Unconscious";

    public override void Initialize()
    {
        SubscribeLocalEvent<StunShakeableComponent, InteractHandEvent>(OnStunShakeableInteractHand,
            before: [typeof(InteractionPopupSystem)]);
    }

    private void OnStunShakeableInteractHand(Entity<StunShakeableComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        var user = args.User;
        if (user == args.Target ||
            !TryComp(user, out StunShakeableUserComponent? shakeableUser))
        {
            return;
        }

        var target = args.Target;
        var rest = CompOrNull<RMCRestComponent>(target);
        var stunned = TryComp(target, out StunnedComponent? stunnedComp) &&
                      stunnedComp.LifeStage <= ComponentLifeStage.Running;
        var knockedDown = TryComp(target, out KnockedDownComponent? knockedDownComp) &&
                          knockedDownComp.LifeStage <= ComponentLifeStage.Running;
        if (!stunned &&
            !knockedDown &&
            !_statusEffects.HasStatusEffect(target, Unconscious) &&
            !HasComp<TackledRecentlyByComponent>(target) &&
            (rest == null || !rest.Resting))
        {
            return;
        }

        args.Handled = true;

        var time = _timing.CurTime;
        if (time < shakeableUser.LastShake + shakeableUser.Cooldown)
            return;

        shakeableUser.LastShake = time;
        Dirty(user, shakeableUser);

        //They fall back down instantly in stam crit
        if (TryComp<RMCStaminaComponent>(ent, out var stamina) && stamina.Level >= 4)
        {
            _popup.PopupClient(Loc.GetString("rmc-shake-awake-stamina", ("target", target)), target, user);
            return;
        }

        _rmcStanding.SetRest(target, false);

        if (_statusEffects.TryRemoveTime(target, Unconscious, ent.Comp.DurationRemoved))
            _sizeStun.TrySyncUnconsciousEffects(target);
        _stun.TryRemoveStunAndKnockdownTime(target, ent.Comp.DurationRemoved);
        RemCompDeferred<TackledRecentlyByComponent>(target);

        var userPopup = Loc.GetString("rmc-shake-awake-user", ("target", target));
        _popup.PopupClient(userPopup, target, user);

        var targetPopup = Loc.GetString("rmc-shake-awake-target", ("user", user));
        _popup.PopupEntity(targetPopup, target, target);

        if (_net.IsServer)
            _audio.PlayEntity(ent.Comp.ShakeSound, Filter.Pvs(target), target, false);

        var othersPopup = Loc.GetString("rmc-shake-awake-others", ("user", user), ("target", target));
        var others = Filter.PvsExcept(target).RemovePlayerByAttachedEntity(user);
        _popup.PopupEntity(othersPopup, target, others, true);

        _adminLogs.Add(LogType.RMCStunShake, $"{ToPrettyString(user)} shook {target} out of a stun.");
    }
}
