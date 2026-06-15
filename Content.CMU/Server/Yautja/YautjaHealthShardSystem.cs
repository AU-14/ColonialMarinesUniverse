using Content.Server.Administration.Logs;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.CameraShake;
using Content.Shared._RMC14.Hands;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaHealthShardSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private RMCCameraShakeSystem _cameraShake = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedVirtualItemSystem _virtualItem = default!;


    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaHealthShardComponent, UseInHandEvent>(OnWholeUseInHand);

        SubscribeLocalEvent<YautjaHealthShardHalfComponent, UseInHandEvent>(OnHalfUseInHand);
        SubscribeLocalEvent<YautjaHealthShardHalfComponent, AfterInteractEvent>(OnHalfAfterInteract);
        SubscribeLocalEvent<YautjaHealthShardHalfComponent, YautjaHealthShardUseDoAfterEvent>(OnUseDoAfter);
        SubscribeLocalEvent<YautjaHealthShardHalfComponent, RMCItemDropAttemptEvent>(OnHalfDropAttempt);
        SubscribeLocalEvent<YautjaHealthShardHalfComponent, ThrowItemAttemptEvent>(OnHalfThrowAttempt);
        SubscribeLocalEvent<YautjaHealthShardHalfComponent, FellDownThrowAttemptEvent>(OnHalfFellDownThrowAttempt);
        SubscribeLocalEvent<YautjaHealthShardHalfComponent, ContainerGettingRemovedAttemptEvent>(OnHalfContainerRemoveAttempt);
        SubscribeLocalEvent<YautjaHealthShardHalfComponent, VirtualItemDeletedEvent>(OnHalfVirtualItemDeleted);
    }

    private void OnWholeUseInHand(Entity<YautjaHealthShardComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!HasComp<YautjaComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-shard-no-idea"), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        if (!TryComp(args.User, out HandsComponent? hands))
            return;

        var heldCount = 0;
        foreach (var _ in _hands.EnumerateHeld((args.User, hands)))
        {
            heldCount++;
        }

        if (heldCount > 1)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-shard-need-both-hands"), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        var coords = Transform(args.User).Coordinates;
        _hands.TryDrop((args.User, hands), ent.Owner, checkActionBlocker: false, doDropInteraction: false);
        QueueDel(ent.Owner);

        var half = Spawn(ent.Comp.HalfPrototype, coords);
        _hands.TryPickupAnyHand(args.User, half);
        _virtualItem.TrySpawnVirtualItemInHand(half, args.User);

        if (ent.Comp.SplitSound != null)
            _audio.PlayPvs(ent.Comp.SplitSound, args.User);

        _popup.PopupEntity(Loc.GetString("cmu-yautja-shard-split"), args.User, args.User);
    }

    private void OnHalfUseInHand(Entity<YautjaHealthShardHalfComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var wholeProto = ent.Comp.WholePrototype;
        var mergeSound = ent.Comp.MergeSound;

        _hands.TryDrop((args.User, null), ent.Owner, checkActionBlocker: false, doDropInteraction: false);
        Del(ent.Owner);
        _virtualItem.DeleteInHandsMatching(args.User, ent.Owner);

        var whole = Spawn(wholeProto, Transform(args.User).Coordinates);
        _hands.TryPickupAnyHand(args.User, whole);

        if (mergeSound != null)
            _audio.PlayPvs(mergeSound, args.User);

        _popup.PopupEntity(Loc.GetString("cmu-yautja-shard-merge"), args.User, args.User);
    }

    private void OnHalfAfterInteract(Entity<YautjaHealthShardHalfComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (target != args.User && !HasComp<YautjaComponent>(target))
            return;

        args.Handled = true;

        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.UseDuration,
            new YautjaHealthShardUseDoAfterEvent(), ent.Owner, target, ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnHandChange = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        if (ent.Comp.UseSound != null)
            _audio.PlayPvs(ent.Comp.UseSound, target);

        _popup.PopupEntity(Loc.GetString("cmu-yautja-shard-use-start"), args.User, args.User);
    }

    private void OnUseDoAfter(Entity<YautjaHealthShardHalfComponent> ent, ref YautjaHealthShardUseDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        var user = args.User;
        var target = args.Target ?? user;

        _hands.TryDrop((user, null), ent.Owner, checkActionBlocker: false, doDropInteraction: false);
        Del(ent.Owner);
        _virtualItem.DeleteInHandsMatching(user, ent.Owner);

        _damage.TryChangeDamage(target, ent.Comp.InstantHeal, true, interruptsDoAfters: false);
        _bloodstream.TryModifyBleedAmount((target, null), -35f);

        var solution = new Solution();
        foreach (var (reagent, amount) in ent.Comp.Reagents)
        {
            solution.AddReagent(reagent, amount);
        }

        _bloodstream.TryAddToChemicals((target, null), solution);

        if (ent.Comp.CompleteSound != null)
            _audio.PlayPvs(ent.Comp.CompleteSound, target);

        _cameraShake.ShakeCamera(target, 10, 8);

        _popup.PopupEntity(Loc.GetString("cmu-yautja-shard-use-complete"), target, user);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):player} used a health shard on {ToPrettyString(target):target}");
    }

    private void OnHalfDropAttempt(Entity<YautjaHealthShardHalfComponent> ent, ref RMCItemDropAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnHalfThrowAttempt(Entity<YautjaHealthShardHalfComponent> ent, ref ThrowItemAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnHalfFellDownThrowAttempt(Entity<YautjaHealthShardHalfComponent> ent, ref FellDownThrowAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnHalfContainerRemoveAttempt(EntityUid uid, YautjaHealthShardHalfComponent comp, ContainerGettingRemovedAttemptEvent args)
    {
        if (HasComp<HandsComponent>(args.Container.Owner))
            args.Cancel();
    }

    private void OnHalfVirtualItemDeleted(Entity<YautjaHealthShardHalfComponent> ent, ref VirtualItemDeletedEvent args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        var coords = Transform(args.User).Coordinates;
        _hands.TryDrop((args.User, null), ent.Owner, checkActionBlocker: false, doDropInteraction: false);
        QueueDel(ent.Owner);

        var whole = Spawn(ent.Comp.WholePrototype, coords);
        _hands.TryPickupAnyHand(args.User, whole);

        if (ent.Comp.MergeSound != null)
            _audio.PlayPvs(ent.Comp.MergeSound, args.User);
    }
}
