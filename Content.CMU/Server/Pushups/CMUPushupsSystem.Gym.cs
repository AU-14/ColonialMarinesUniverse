using Content.Server.Chat.Systems;
using Content.Shared.Buckle.Components;
using Content.Shared.Chat;
using Content.Shared.CMU14.Gym;
using Content.Shared.CMU14.Pushups;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Pushups;

/// <summary>
///     Gym equipment ported from cmss13: weight machines, dumbbells and the treadmill run sets the same way pushups do,
///     while the punching bag keeps a session going for as long as the punches keep coming.
/// </summary>
public sealed partial class CMUPushupsSystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    private const int PunchAnnounceEvery = 10;

    public override void Initialize()
    {
        SubscribeLocalEvent<HumanoidProfileComponent, EmoteEvent>(OnEmote);
        SubscribeLocalEvent<CMUPushupsComponent, MoveInputEvent>(OnMoveInput);
        SubscribeLocalEvent<HumanoidProfileComponent, CMUPushupsStyleChosenEvent>(OnPushupsStyleChosen);
        SubscribeLocalEvent<CMUPushupsComponent, CMUPushupsFinishEvent>(OnPushupsFinish);

        SubscribeLocalEvent<CMUExerciseMachineComponent, StrappedEvent>(OnMachineStrapped);
        SubscribeLocalEvent<CMUExerciseMachineComponent, UnstrappedEvent>(OnMachineUnstrapped);
        SubscribeLocalEvent<CMUDumbbellComponent, UseInHandEvent>(OnDumbbellUseInHand);
        SubscribeLocalEvent<CMUPunchingBagComponent, AttackedEvent>(OnPunchingBagAttacked);
        SubscribeLocalEvent<CMUTreadmillComponent, MapInitEvent>(OnTreadmillMapInit);
        SubscribeLocalEvent<CMUTreadmillComponent, ComponentShutdown>(OnTreadmillShutdown);
        SubscribeLocalEvent<CMUTreadmillComponent, InteractHandEvent>(OnTreadmillInteractHand);
    }

    private void OnMachineStrapped(Entity<CMUExerciseMachineComponent> ent, ref StrappedEvent args)
    {
        var user = args.Buckle.Owner;
        if (!HasComp<HumanoidProfileComponent>(user))
            return;

        if (!TryStart(user, ent.Comp.Exercise, ent))
            _buckle.Unbuckle((user, args.Buckle.Comp), user);
    }

    private void OnMachineUnstrapped(Entity<CMUExerciseMachineComponent> ent, ref UnstrappedEvent args)
    {
        var user = args.Buckle.Owner;
        if (!TryComp<CMUPushupsComponent>(user, out var comp) || !comp.Active || comp.Equipment != ent.Owner)
            return;

        Stop((user, comp), false);
        _popup.PopupEntity(Loc.GetString(Key(comp.Exercise, "stop"), ("count", comp.Count)), user, user);
    }

    private void OnDumbbellUseInHand(Entity<CMUDumbbellComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !HasComp<HumanoidProfileComponent>(args.User))
            return;

        args.Handled = true;
        if (TryComp<CMUPushupsComponent>(args.User, out var comp) && comp.Active)
        {
            if (comp.Equipment != ent.Owner)
                return;

            Stop((args.User, comp), false);
            _popup.PopupEntity(Loc.GetString(Key(comp.Exercise, "stop"), ("count", comp.Count)), args.User, args.User);
            return;
        }

        TryStart(args.User, CMUExercise.Curls, ent);
    }

    /// <summary>Each unarmed punch thrown at the bag counts, until they're winded.</summary>
    private void OnPunchingBagAttacked(Entity<CMUPunchingBagComponent> ent, ref AttackedEvent args)
    {
        var user = args.User;
        if (args.Used != user || !HasComp<HumanoidProfileComponent>(user))
            return;

        var now = _timing.CurTime;
        var comp = EnsureComp<CMUPushupsComponent>(user);
        if (comp.Active || comp.PunchTiredUntil is { } tired && now < tired)
            return;

        _audio.PlayPvs(ent.Comp.HitSound, ent);
        ent.Comp.SwingUntil = now + ent.Comp.SwingTime;
        _appearance.SetData(ent, CMUPunchingBagVisuals.Swinging, true);

        if (AddSessionEffort((user, comp), CMUExercise.Punching, 1, now))
        {
            comp.PunchTiredUntil = now + ent.Comp.TiredTime;
            Dirty(user, comp);
            return;
        }

        var punches = (int) comp.SessionAmount;
        if (punches / PunchAnnounceEvery > comp.SessionAnnounced)
        {
            comp.SessionAnnounced = punches / PunchAnnounceEvery;
            SendCount(user, Loc.GetString("cmu-punching-count", ("count", punches)));
        }
    }

    private void OnTreadmillMapInit(Entity<CMUTreadmillComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Handles is { } handles)
            SpawnAttachedTo(handles, new EntityCoordinates(ent, default));

        if (ent.Comp.BackHalf is not { } backHalf || ent.Comp.Other != null)
            return;

        var xform = Transform(ent);
        var behind = xform.Coordinates.Offset(-xform.LocalRotation.ToWorldVec());
        // Its prototype anchors it; anchoring by hand here re-enters map init while a map is still loading.
        var back = SpawnAttachedTo(backHalf, behind, rotation: xform.LocalRotation);

        var backComp = EnsureComp<CMUTreadmillComponent>(back);
        backComp.Other = ent;
        ent.Comp.Other = back;
        Dirty(back, backComp);
        Dirty(ent);
    }

    private void OnTreadmillShutdown(Entity<CMUTreadmillComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Other is { } other && !TerminatingOrDeleted(other))
            QueueDel(other);
    }

    /// <summary>Clicking either half gets them on and running until they can't anymore.</summary>
    private void OnTreadmillInteractHand(Entity<CMUTreadmillComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || !HasComp<HumanoidProfileComponent>(args.User))
            return;

        args.Handled = true;
        var console = ent.Comp.BackHalf != null ? ent.Owner : ent.Comp.Other;
        if (console is not { } front || !TryComp<CMUTreadmillComponent>(front, out var treadmill))
            return;

        if (treadmill.Running)
        {
            _popup.PopupEntity(Loc.GetString("cmu-treadmill-in-use", ("treadmill", front)), front, args.User);
            return;
        }

        var user = args.User;
        if (TryComp<CMUPushupsComponent>(user, out var comp) && comp.Active)
            return;

        // Onto the belt, facing the console.
        var xform = Transform(front);
        _transform.SetCoordinates(user, xform.Coordinates);
        _transform.SetLocalRotation(user, xform.LocalRotation);
        if (!TryStart(user, CMUExercise.Running, front))
            return;

        _audio.PlayPvs(treadmill.ToggleSound, front);
    }

    private void SetTreadmillRunning(Entity<CMUTreadmillComponent> ent, bool running)
    {
        SetTreadmillHalfRunning(ent, running);
        if (ent.Comp.Other is { } other && TryComp<CMUTreadmillComponent>(other, out var otherComp))
            SetTreadmillHalfRunning((other, otherComp), running);
    }

    private void SetTreadmillHalfRunning(Entity<CMUTreadmillComponent> ent, bool running)
    {
        ent.Comp.Running = running;
        Dirty(ent);
        _appearance.SetData(ent, CMUTreadmillVisuals.Running, running);
    }

    private void UpdateGym(TimeSpan now)
    {
        var bags = EntityQueryEnumerator<CMUPunchingBagComponent>();
        while (bags.MoveNext(out var uid, out var bag))
        {
            if (bag.SwingUntil != TimeSpan.Zero && now >= bag.SwingUntil)
            {
                bag.SwingUntil = TimeSpan.Zero;
                _appearance.SetData(uid, CMUPunchingBagVisuals.Swinging, false);
            }
        }
    }

    /// <summary>
    ///     Adds punches thrown to a session that keeps going while the effort keeps coming.
    ///     Returns true once the body gives out, which ends the session.
    /// </summary>
    private bool AddSessionEffort(Entity<CMUPushupsComponent> ent, CMUExercise exercise, float amount, TimeSpan now)
    {
        var comp = ent.Comp;
        if (comp.SessionExercise != exercise || now > comp.SessionLast + comp.SessionTimeout)
        {
            EndSession(comp);
            comp.SessionExercise = exercise;
            comp.SessionAmount = 0;
            comp.SessionAnnounced = 0;
            (comp.SessionLimit, comp.SessionRestedLimit) = RollLimits(ent, comp, exercise, now);
        }

        comp.SessionAmount += amount;
        comp.SessionLast = now;
        if (comp.SessionAmount < comp.SessionLimit)
            return false;

        var done = (int) comp.SessionAmount;
        comp.SessionExercise = null;
        AddFatigue(comp, exercise, 1f, now);
        _chat.TrySendInGameICMessage(ent,
            Loc.GetString(Key(exercise, "fail-emote"), ("count", done)),
            InGameICChatType.Emote,
            ChatTransmitRange.Normal,
            hideLog: true,
            ignoreActionBlocker: true);
        _popup.PopupEntity(Loc.GetString(Key(exercise, "fail"), ("count", done)), ent, ent, PopupType.MediumCaution);
        return true;
    }

    /// <summary>A session that was walked away from still tires them out, by how much of it they got through.</summary>
    private static void EndSession(CMUPushupsComponent comp)
    {
        if (comp.SessionExercise is not { } previous)
            return;

        comp.SessionExercise = null;
        AddFatigue(comp, previous, comp.SessionAmount / Math.Max(1, comp.SessionRestedLimit), comp.SessionLast);
    }
}
