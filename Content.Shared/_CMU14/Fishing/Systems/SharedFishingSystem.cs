// SPDX-FileCopyrightText: 2025 Aidenkrz <aiden@djkraz.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Roudenn <romabond091@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._CMU14.Fishing.Components;
using Content.Shared._CMU14.Fishing.Events;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Content.Shared.Actions.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.Actions;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Fishing.Systems;

/// <summary>
/// This handles... da fish
/// </summary>
public abstract partial class SharedFishingSystem : EntitySystem
{
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected ThrowingSystem Throwing = default!;
    [Dependency] protected SharedTransformSystem Xform = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    protected EntityQuery<ActiveFisherComponent> FisherQuery;
    protected EntityQuery<ActiveFishingSpotComponent> ActiveFishSpotQuery;
    protected EntityQuery<FishingSpotComponent> FishSpotQuery;
    protected EntityQuery<FishingRodComponent> FishRodQuery;
    protected EntityQuery<FishingLureComponent> FishLureQuery;
    protected EntityQuery<ActionComponent> ActionQuery;
    protected EntityQuery<ActionsContainerComponent> ActionsContainerQuery;
    protected EntityQuery<TransformComponent> XformQuery;

    public override void Initialize()
    {
        base.Initialize();

        FisherQuery = GetEntityQuery<ActiveFisherComponent>();
        ActiveFishSpotQuery = GetEntityQuery<ActiveFishingSpotComponent>();
        FishSpotQuery = GetEntityQuery<FishingSpotComponent>();
        FishRodQuery = GetEntityQuery<FishingRodComponent>();
        FishLureQuery = GetEntityQuery<FishingLureComponent>();
        ActionQuery = GetEntityQuery<ActionComponent>();
        ActionsContainerQuery = GetEntityQuery<ActionsContainerComponent>();
        XformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<FishingRodComponent, MapInitEvent>(OnFishingRodInit);
        SubscribeLocalEvent<FishingRodComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<FishingRodComponent, GotEquippedHandEvent>(OnRodEquippedHand);
        SubscribeLocalEvent<FishingRodComponent, GotUnequippedHandEvent>(OnRodUnequippedHand);
        SubscribeLocalEvent<FishingRodComponent, ThrowFishingLureActionEvent>(OnThrowFloat);
        SubscribeLocalEvent<FishingRodComponent, PullFishingLureActionEvent>(OnPullFloat);
        SubscribeLocalEvent<FishingRodComponent, EntParentChangedMessage>(OnRodParentChanged);

        SubscribeLocalEvent<FishingRodComponent, EntityTerminatingEvent>(OnRodTerminating);
        SubscribeLocalEvent<FishingLureComponent, EntityTerminatingEvent>(OnLureTerminating);
        SubscribeLocalEvent<ActiveFishingSpotComponent, EntityTerminatingEvent>(OnSpotTerminating);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateFishing();
    }

    private void UpdateFishing()
    {
        if (!Timing.IsFirstTimePredicted)
            return;

        var currentTime = Timing.CurTime;
        var activeFishers = EntityQueryEnumerator<ActiveFisherComponent>();
        while (activeFishers.MoveNext(out var fisher, out var fisherComp))
        {
            // Get fishing rod, then float, then spot... ReCurse.
            if (!TryGetFishingRod(fisherComp.FishingRod, out var fishRod) ||
                !TryGetFishingLure(fishRod.Comp.FishingLure, out var fishingFloat) ||
                fishingFloat.Comp.AttachedEntity is not { } fishSpot ||
                !fishSpot.IsValid() ||
                !Exists(fishSpot) ||
                !ActiveFishSpotQuery.TryComp(fishSpot, out var activeSpotComp))
                continue;

            fisherComp.TotalProgress ??= fishRod.Comp.StartingProgress;
            fisherComp.NextStruggle ??= Timing.CurTime + TimeSpan.FromSeconds(fishRod.Comp.StartingStruggleTime);

            // Fish fighting logic
            CalculateFightingTimings((fisher ,fisherComp), activeSpotComp);

            switch (fisherComp.TotalProgress)
            {
                case < 0f:
                    // It's over
                    _popup.PopupEntity(Loc.GetString("fishing-progress-fail"), fisher, fisher);
                    StopFishing(fishRod, fisher);
                    continue;

                case >= 1f:
                    if (activeSpotComp.Fish != null)
                    {
                        ThrowFishReward(activeSpotComp.Fish.Value, fishSpot, fisher);
                        _popup.PopupEntity(Loc.GetString("fishing-progress-success"), fisher, fisher);
                    }

                    StopFishing(fishRod, fisher);
                    break;
            }
        }

        var fishingSpots = EntityQueryEnumerator<ActiveFishingSpotComponent>();
        while (fishingSpots.MoveNext(out var activeSpotComp))
        {
            if (currentTime < activeSpotComp.FishingStartTime || activeSpotComp.IsActive || activeSpotComp.FishingStartTime == null)
                continue;

            // Get fishing lure, then rod, then player... ReCurse.
            if (!TryGetFishingLure(activeSpotComp.AttachedFishingLure, out var fishingFloat) ||
                TerminatingOrDeleted(fishingFloat) ||
                !TryGetFishingRod(fishingFloat.Comp.FishingRod, out var fishRod))
                continue;

            var fisher = Transform(fishRod).ParentUid;

            var activeFisher = EnsureComp<ActiveFisherComponent>(fisher);
            activeFisher.FishingRod = fishRod;
            activeFisher.ProgressPerUse *= fishRod.Comp.Efficiency;
            activeFisher.TotalProgress = fishRod.Comp.StartingProgress;
            activeFisher.NextStruggle = Timing.CurTime + TimeSpan.FromSeconds(fishRod.Comp.StartingStruggleTime); // Compensate ping for 0.3 seconds

            // Predicted because it works like 99.9% of the time anyway.
            _popup.PopupPredicted(Loc.GetString("fishing-progress-start"), fisher, fisher);
            activeSpotComp.IsActive = true;
        }

        var fishingLures = EntityQueryEnumerator<FishingLureComponent>();
        while (fishingLures.MoveNext(out var fishingLure, out var lureComp))
        {
            if (lureComp.NextUpdate > Timing.CurTime)
                continue;

            lureComp.NextUpdate = Timing.CurTime + TimeSpan.FromSeconds(lureComp.UpdateInterval);

            if (!TryGetFishingRod(lureComp.FishingRod, out var fishingRod))
                continue;

            var lurePos = Xform.GetMapCoordinates(fishingLure);
            var rodPos = Xform.GetMapCoordinates(fishingRod);
            var distance = lurePos.Position - rodPos.Position;
            var fisher = Transform(fishingRod).ParentUid;

            if (distance.Length() > fishingRod.Comp.BreakOnDistance ||
                lurePos.MapId != rodPos.MapId ||
                !_hands.IsHolding(fisher, fishingRod) ||
                !HasComp<ActorComponent>(fisher))
            {
                StopFishing(fishingRod, fisher);
            }
        }
    }

    /// <summary>
    /// if AddPulling is true, we ADD Pulling action and REMOVE Throwing action.
    /// Basically true if we start, and false if we end.
    /// </summary>
    private void ToggleFishingActions(Entity<FishingRodComponent> ent, EntityUid fisher, bool addPulling)
    {
        if (addPulling)
        {
            _actions.RemoveAction(ent.Comp.ThrowLureActionEntity);
            ResetFishingActionIfMoved(ent, ref ent.Comp.PullLureActionEntity);
            _actions.AddAction(fisher, ref ent.Comp.PullLureActionEntity, ent.Comp.PullLureActionId, ent);
        }
        else
        {
            _actions.RemoveAction(ent.Comp.PullLureActionEntity);
            ResetFishingActionIfMoved(ent, ref ent.Comp.ThrowLureActionEntity);
            _actions.AddAction(fisher, ref ent.Comp.ThrowLureActionEntity, ent.Comp.ThrowLureActionId, ent);
        }
    }

    private void ResetFishingActionIfMoved(Entity<FishingRodComponent> rod, ref EntityUid? actionId)
    {
        if (actionId is not { } action)
            return;

        if (!action.IsValid() ||
            !Exists(action) ||
            !ActionQuery.TryComp(action, out var actionComp) ||
            !ActionsContainerQuery.TryComp(rod.Owner, out var containerComp) ||
            actionComp.Container != rod.Owner ||
            !XformQuery.TryComp(action, out var actionXform))
        {
            actionId = null;
            return;
        }

        var container = containerComp.Container;
        if (!container.Contains(action) ||
            !_container.IsEntityInContainer(action) ||
            actionXform.ParentUid != rod.Owner)
        {
            actionId = null;
        }
    }

    protected abstract void CalculateFightingTimings(Entity<ActiveFisherComponent> fisher, ActiveFishingSpotComponent activeSpotComp);

    /// <summary>
    /// Server-side only, sets up fishing float and throws it
    /// </summary>
    protected abstract void SetupFishingFloat(Entity<FishingRodComponent> fishingRod, EntityUid player, EntityCoordinates target);

    /// <summary>
    /// Server-side only, spawns a fish and throws it to our player!
    /// </summary>
    protected abstract void ThrowFishReward(EntProtoId fishId, EntityUid fishSpot, EntityUid target);

    /// <summary>
    /// Reels the fishing rod back and stops fishing progress if arguments are passed to it.
    /// Server also deletes Fishing Lure
    /// </summary>
    protected virtual void StopFishing(
        Entity<FishingRodComponent> fishingRod,
        EntityUid? fisher)
    {
        if (fishingRod.Comp.FishingLure is { } lure)
        {
            if (TryGetFishingLure(lure, out var lureEnt) &&
                lureEnt.Comp.AttachedEntity is { } attachedEntity &&
                attachedEntity.IsValid() &&
                Exists(attachedEntity) &&
                ActiveFishSpotQuery.TryComp(attachedEntity, out var activeSpotComp))
            {
                RemCompDeferred(attachedEntity, activeSpotComp);
            }

            fishingRod.Comp.FishingLure = null;
            Dirty(fishingRod);
        }

        if (fisher != null && Exists(fisher.Value))
        {
            if (FisherQuery.TryComp(fisher.Value, out var fisherComp))
                RemCompDeferred(fisher.Value, fisherComp);

            if (!TerminatingOrDeleted(fishingRod.Owner) && _hands.IsHolding(fisher.Value, fishingRod))
                ToggleFishingActions(fishingRod, fisher.Value, false);
        }
    }

    #region Terminating Events

    private void OnRodTerminating(Entity<FishingRodComponent> ent, ref EntityTerminatingEvent args)
    {
        TryStopFishing(ent);
    }

    private void OnLureTerminating(Entity<FishingLureComponent> ent, ref EntityTerminatingEvent args)
    {
        TryStopFishing(ent);
    }

    private void OnSpotTerminating(Entity<ActiveFishingSpotComponent> ent, ref EntityTerminatingEvent args)
    {
        TryStopFishing(ent);
    }

    #endregion

    #region Deletion Helpers

    private bool TryGetFishingRod(EntityUid uid, out Entity<FishingRodComponent> rod)
    {
        rod = default;

        if (!uid.IsValid() ||
            !Exists(uid) ||
            !FishRodQuery.TryComp(uid, out var rodComp))
        {
            return false;
        }

        rod = (uid, rodComp);
        return true;
    }

    private bool TryGetFishingLure(EntityUid? uid, out Entity<FishingLureComponent> lure)
    {
        lure = default;

        if (uid is not { } lureUid ||
            !lureUid.IsValid() ||
            !Exists(lureUid) ||
            !FishLureQuery.TryComp(lureUid, out var lureComp))
        {
            return false;
        }

        lure = (lureUid, lureComp);
        return true;
    }

    /// <summary>
    /// Stops fishing by taking only the Fishing rod as an argument.
    /// </summary>
    private void TryStopFishing(Entity<FishingRodComponent> rod)
    {
        var player = Transform(rod).ParentUid;
        StopFishing(rod, player);
    }

    /// <summary>
    /// Stops fishing by taking only the Fishing lure as an argument.
    /// </summary>
    private void TryStopFishing(Entity<FishingLureComponent> lure)
    {
        if (!TryGetFishingRod(lure.Comp.FishingRod, out var rod))
            return;

        TryStopFishing(rod);
    }

    /// <summary>
    /// Stops fishing by taking only the Active spot as an argument.
    /// </summary>
    private void TryStopFishing(Entity<ActiveFishingSpotComponent> spot)
    {
        if (!TryGetFishingLure(spot.Comp.AttachedFishingLure, out var lure))
            return;

        if (!TryGetFishingRod(lure.Comp.FishingRod, out var rod))
            return;

        TryStopFishing(rod);
    }

    #endregion

    #region Event Handling

    private void OnThrowFloat(Entity<FishingRodComponent> ent, ref ThrowFishingLureActionEvent args)
    {
        if (args.Handled || !Timing.IsFirstTimePredicted)
            return;

        var player = args.Performer;

        if (ent.Comp.FishingLure != null || !Xform.IsValid(args.Target))
        {
            args.Handled = true;
            return;
        }

        SetupFishingFloat(ent, player, args.Target);
        ToggleFishingActions(ent, player, true);
        args.Handled = true;
    }

    private void OnPullFloat(Entity<FishingRodComponent> ent, ref PullFishingLureActionEvent args)
    {
        if (args.Handled || !Timing.IsFirstTimePredicted)
            return;

        var player = args.Performer;
        var (uid, component) = ent;

        if (component.FishingLure == null)
        {
            ToggleFishingActions(ent, player, false);
            args.Handled = true;
            return;
        }

        _popup.PopupPredicted(Loc.GetString("fishing-rod-remove-lure", ("ent", Name(uid))), uid, uid);

        if (!TryGetFishingLure(component.FishingLure, out var lure))
        {
            StopFishing(ent, player);
            args.Handled = true;
            return;
        }

        if (lure.Comp.AttachedEntity is { } attachedEnt &&
            attachedEnt.IsValid() &&
            Exists(attachedEnt))
        {
            // TODO: so this kinda just lets you pull anything right up to you, it should instead just apply an impulse in your direction modfiied by the weight of the player vs the object
            // Also we need to autoreel/snap the line if the player gets too far away
            // Also we should probably PVS override the lure if the rod is in PVS, and vice versa to stop the joint visuals from popping in/out
            var targetCoords = Xform.GetMapCoordinates(Transform(attachedEnt));
            var playerCoords = Xform.GetMapCoordinates(Transform(player));
            var rand = new RobustRandom(); // evil random prediction hack
            rand.SetSeed((int) Timing.CurTick.Value);

            // Calculate throw direction
            var multiplier = 0.2f + rand.NextFloat() * (0.85f - 0.2f);
            var direction = (playerCoords.Position - targetCoords.Position) * multiplier;

            // Yeet
            Throwing.TryThrow(attachedEnt, direction, 4f, player);
        }

        StopFishing(ent, player);
        args.Handled = true;
    }

    private void OnFishingRodInit(Entity<FishingRodComponent> ent, ref MapInitEvent args)
    {
        ResetFishingActionIfMoved(ent, ref ent.Comp.ThrowLureActionEntity);
        _actions.AddAction(ent, ref ent.Comp.ThrowLureActionEntity, ent.Comp.ThrowLureActionId);
    }

    private void OnRodParentChanged(Entity<FishingRodComponent> ent, ref EntParentChangedMessage args)
    {
        // Anything that is an active fisher should be fine.
        if (!FisherQuery.HasComp(args.Transform.ParentUid))
        {
            StopFishing(ent, args.OldParent);
        }
    }

    private void OnGetActions(Entity<FishingRodComponent> ent, ref GetItemActionsEvent args)
    {
        if (ent.Comp.FishingLure == null)
        {
            ResetFishingActionIfMoved(ent, ref ent.Comp.ThrowLureActionEntity);
            args.AddAction(ref ent.Comp.ThrowLureActionEntity, ent.Comp.ThrowLureActionId);
        }
        else
        {
            ResetFishingActionIfMoved(ent, ref ent.Comp.PullLureActionEntity);
            args.AddAction(ref ent.Comp.PullLureActionEntity, ent.Comp.PullLureActionId);
        }
    }

    private void OnRodEquippedHand(Entity<FishingRodComponent> ent, ref GotEquippedHandEvent args)
    {
        if (ent.Comp.FishingLure == null)
        {
            ResetFishingActionIfMoved(ent, ref ent.Comp.ThrowLureActionEntity);
            _actions.AddAction(args.User, ref ent.Comp.ThrowLureActionEntity, ent.Comp.ThrowLureActionId, ent);
        }
        else
        {
            ResetFishingActionIfMoved(ent, ref ent.Comp.PullLureActionEntity);
            _actions.AddAction(args.User, ref ent.Comp.PullLureActionEntity, ent.Comp.PullLureActionId, ent);
        }
    }

    private void OnRodUnequippedHand(Entity<FishingRodComponent> ent, ref GotUnequippedHandEvent args)
    {
        _actions.RemoveProvidedActions(args.User, ent);
    }

    #endregion
}
