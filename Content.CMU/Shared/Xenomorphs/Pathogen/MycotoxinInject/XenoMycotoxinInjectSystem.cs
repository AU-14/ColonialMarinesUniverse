using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared._CMU14.Xenomorphs.Pathogen.Walker;
using Content.Shared.DoAfter;
using Content.Shared.Jittering;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Network;

namespace Content.Shared._CMU14.Xenomorphs.Pathogen.MycotoxinInject;

public sealed partial class CMUXenoMycotoxinInjectSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly XenoPlasmaSystem _xenoPlasma = default!;
    [Dependency] private readonly XenoSystem _xeno = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUXenoMycotoxinInjectComponent, CMUXenoMycotoxinInjectActionEvent>(OnAction);
        SubscribeLocalEvent<CMUXenoMycotoxinInjectComponent, CMUXenoMycotoxinInjectDoAfterEvent>(OnDoAfter);
    }

    private void OnAction(Entity<CMUXenoMycotoxinInjectComponent> xeno, ref CMUXenoMycotoxinInjectActionEvent args)
    {
        if (args.Handled)
            return;

        var target = args.Target;

        if (TerminatingOrDeleted(target))
        {
            _popup.PopupClient(Loc.GetString("cmu14-mycotoxin-inject-invalid"), xeno, xeno, PopupType.SmallCaution);
            return;
        }

        if (!ValidateTarget(xeno, target, out _))
            return;

        if (!_xenoPlasma.HasPlasmaPopup(xeno.Owner, xeno.Comp.PlasmaCost))
            return;

        args.Handled = true;

        var selfMsg = Loc.GetString("cmu14-mycotoxin-inject-start-self", ("target", (object) target));
        _popup.PopupClient(selfMsg, xeno.Owner, xeno.Owner, PopupType.MediumCaution);

        var targetMsg = Loc.GetString("cmu14-mycotoxin-inject-start-target", ("xeno", (object) xeno.Owner));
        _popup.PopupEntity(targetMsg, target, target, PopupType.MediumCaution);

        var doAfter = new DoAfterArgs(EntityManager, xeno, xeno.Comp.Delay, new CMUXenoMycotoxinInjectDoAfterEvent(), xeno, target)
        {
            BreakOnMove = true,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
            NeedHand = false,
        };

        _doAfter.TryStartDoAfter(doAfter);
        _jitter.DoJitter(target, xeno.Comp.Delay, true, 14f, 5f, true);
    }

    private void OnDoAfter(Entity<CMUXenoMycotoxinInjectComponent> xeno, ref CMUXenoMycotoxinInjectDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        if (TerminatingOrDeleted(target))
            return;

        // Re-validate: the target could've died, been rescued, or already been turned
        // by someone else during the channel.
        if (!ValidateTarget(xeno, target, out _))
            return;

        if (!_xenoPlasma.TryRemovePlasmaPopup((xeno.Owner, null), xeno.Comp.PlasmaCost))
            return;

        args.Handled = true;

        _popup.PopupPredicted(
            Loc.GetString("cmu14-mycotoxin-inject-self", ("target", (object) target)),
            Loc.GetString("cmu14-mycotoxin-inject-target", ("xeno", (object) xeno.Owner)),
            xeno, xeno, PopupType.MediumCaution);

        if (_net.IsServer)
            RaiseLocalEvent(new CMUMycotoxinInjectDoReanimateEvent(target, xeno.Owner));
    }

    /// <summary>Shared validity check used both when starting and finishing the channel.</summary>
    private bool ValidateTarget(Entity<CMUXenoMycotoxinInjectComponent> xeno, EntityUid target, out bool isDead)
    {
        isDead = _mobState.IsDead(target);
        var validState = isDead || xeno.Comp.CanInjectLiving;

        if (!validState)
        {
            _popup.PopupClient(Loc.GetString("cmu14-mycotoxin-inject-not-dead"), xeno, xeno, PopupType.SmallCaution);
            return false;
        }

        if (HasComp<XenoComponent>(target))
        {
            _popup.PopupClient(Loc.GetString("cmu14-mycotoxin-inject-invalid"), xeno, xeno, PopupType.SmallCaution);
            return false;
        }

        if (HasComp<VictimInfectedComponent>(target) || HasComp<CMUPathogenWalkerComponent>(target))
        {
            _popup.PopupClient(Loc.GetString("cmu14-mycotoxin-inject-already-infected"), xeno, xeno, PopupType.SmallCaution);
            return false;
        }

        return true;
    }
}

public sealed class CMUMycotoxinInjectDoReanimateEvent : EntityEventArgs
{
    public EntityUid Target;
    public EntityUid Injector;
    public CMUMycotoxinInjectDoReanimateEvent(EntityUid target, EntityUid injector)
    {
        Target = target;
        Injector = injector;
    }
}