using Content.Server.Administration.Logs;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.StepTrigger.Components;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Toggleable;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Utility;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaTrapSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private StepTriggerSystem _stepTrigger = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private YautjaRitualSystem _ritual = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaTrapComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<YautjaTrapComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<YautjaTrapComponent, GetVerbsEvent<InteractionVerb>>(OnGetInteractionVerbs);
        SubscribeLocalEvent<YautjaTrapComponent, GettingPickedUpAttemptEvent>(OnGettingPickedUpAttempt);
        SubscribeLocalEvent<YautjaTrapComponent, StepTriggerAttemptEvent>(OnStepTriggerAttempt);
        SubscribeLocalEvent<YautjaTrapComponent, StepTriggeredOnEvent>(OnStepTriggeredOn);
    }

    private void OnUseInHand(Entity<YautjaTrapComponent> trap, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryArmTrap(trap, args.User);
    }

    private void OnInteractHand(Entity<YautjaTrapComponent> trap, ref InteractHandEvent args)
    {
        if (args.Handled || !trap.Comp.Armed || !HasComp<YautjaComponent>(args.User))
            return;

        args.Handled = TryRecoverTrap(trap, args.User);
    }

    private void OnGetInteractionVerbs(Entity<YautjaTrapComponent> trap, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!trap.Comp.Armed ||
            !args.CanAccess ||
            !args.CanInteract ||
            !HasComp<YautjaComponent>(args.User))
        {
            return;
        }

        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("cmu-yautja-trap-recover-verb"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/pickup.svg.192dpi.png")),
            Act = () => TryRecoverTrap(trap, user),
        });
    }

    private void OnGettingPickedUpAttempt(Entity<YautjaTrapComponent> trap, ref GettingPickedUpAttemptEvent args)
    {
        if (!trap.Comp.Armed)
            return;

        if (HasComp<YautjaComponent>(args.User))
        {
            TryDisarmTrap(trap, args.User);
            return;
        }

        args.Cancel();
        TryTriggerTrap(trap, args.User);
    }

    private void OnStepTriggerAttempt(Entity<YautjaTrapComponent> trap, ref StepTriggerAttemptEvent args)
    {
        args.Continue = true;

        if (!CanTriggerTrap(trap, args.Tripper))
            args.Cancelled = true;
    }

    private void OnStepTriggeredOn(Entity<YautjaTrapComponent> trap, ref StepTriggeredOnEvent args)
    {
        TryTriggerTrap(trap, args.Tripper);
    }

    public bool TryArmTrap(Entity<YautjaTrapComponent> trap, EntityUid user)
    {
        if (!HasComp<YautjaComponent>(user))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-tech-denied"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (trap.Comp.Armed)
            return true;

        if (_hands.IsHolding(user, trap.Owner) && !_hands.TryDrop(user, trap.Owner))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-trap-arm-failed"), user, user, PopupType.SmallCaution);
            return false;
        }

        trap.Comp.TrapOwner = user;
        trap.Comp.Armed = true;
        Dirty(trap);

        var xform = Transform(trap);
        _transform.AnchorEntity(trap, xform);

        if (TryComp<PhysicsComponent>(trap, out var physics))
            _physics.SetBodyType(trap, BodyType.Static, body: physics);

        if (TryComp<StepTriggerComponent>(trap, out var trigger))
            _stepTrigger.SetActive(trap, true, trigger);

        _appearance.SetData(trap, ToggleableVisuals.Enabled, true);
        _audio.PlayPvs(trap.Comp.ArmSound, trap);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-trap-armed"), user, user);
        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(user):player} armed Yautja hunting trap {ToPrettyString(trap.Owner):trap}");
        return true;
    }

    public bool TryDisarmTrap(Entity<YautjaTrapComponent> trap, EntityUid user)
    {
        if (!trap.Comp.Armed)
            return true;

        if (!HasComp<YautjaComponent>(user))
            return false;

        trap.Comp.Armed = false;
        Dirty(trap);

        _transform.Unanchor(trap);

        if (TryComp<PhysicsComponent>(trap, out var physics))
            _physics.SetBodyType(trap, BodyType.Dynamic, body: physics);

        if (TryComp<StepTriggerComponent>(trap, out var trigger))
            _stepTrigger.SetActive(trap, false, trigger);

        _appearance.SetData(trap, ToggleableVisuals.Enabled, false);
        _audio.PlayPvs(trap.Comp.DisarmSound, trap);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-trap-disarmed"), user, user);
        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(user):player} disarmed Yautja hunting trap {ToPrettyString(trap.Owner):trap}");
        return true;
    }

    public bool TryRecoverTrap(Entity<YautjaTrapComponent> trap, EntityUid user)
    {
        if (!trap.Comp.Armed || !HasComp<YautjaComponent>(user))
            return false;

        if (!_hands.TryGetEmptyHand(user, out _))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-trap-recover-no-hand"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (!TryDisarmTrap(trap, user))
            return false;

        if (!_hands.TryPickupAnyHand(user, trap.Owner, checkActionBlocker: false))
            return false;

        _popup.PopupEntity(Loc.GetString("cmu-yautja-trap-recovered"), user, user);
        return true;
    }

    public bool TryTriggerTrap(Entity<YautjaTrapComponent> trap, EntityUid tripper)
    {
        if (!CanTriggerTrap(trap, tripper))
            return false;

        trap.Comp.Armed = false;
        Dirty(trap);

        _damage.TryChangeDamage(tripper, trap.Comp.Damage, true, origin: trap.Comp.TrapOwner ?? trap.Owner, tool: trap.Owner);
        _stun.TryParalyze(tripper, trap.Comp.ParalyzeTime, true);
        if (trap.Comp.TrapOwner is { } trapOwner)
            _ritual.TryClaimCaptive(trapOwner, tripper, true);

        _audio.PlayPvs(trap.Comp.TriggerSound, trap);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-trap-triggered"), tripper, tripper, PopupType.MediumCaution);
        var ownerName = trap.Comp.TrapOwner is { } ownerUid ? ToPrettyString(ownerUid) : "unknown owner";
        _adminLog.Add(LogType.Action, LogImpact.Medium, $"Yautja hunting trap {ToPrettyString(trap.Owner):trap} owned by {ownerName} triggered on {ToPrettyString(tripper):target}");

        QueueDel(trap);
        return true;
    }

    private bool CanTriggerTrap(Entity<YautjaTrapComponent> trap, EntityUid tripper)
    {
        if (!trap.Comp.Armed ||
            Deleted(tripper) ||
            tripper == trap.Comp.TrapOwner ||
            HasComp<YautjaComponent>(tripper) ||
            !TryComp<MobStateComponent>(tripper, out var mobState) ||
            !_mobState.IsAlive(tripper, mobState))
        {
            return false;
        }

        return true;
    }
}
