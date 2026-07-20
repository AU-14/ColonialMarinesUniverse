using Content.Shared._RMC14.Stack;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.DoAfter;
using Content.Shared.Emp;
using Content.Shared.Examine;
using Content.Shared.Explosion.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Light.Components;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;

    // RMC14
    [Dependency] private SharedRMCStackSystem _rmcStack = default!;

    [MustCallBase]
    protected virtual void InitializeBallistic()
    {
        SubscribeLocalEvent<BallisticAmmoProviderComponent, ComponentInit>(OnBallisticInit);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, MapInitEvent>(OnBallisticMapInit);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, TakeAmmoEvent>(OnBallisticTakeAmmo);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, GetAmmoCountEvent>(OnBallisticAmmoCount);

        SubscribeLocalEvent<BallisticAmmoProviderComponent, ExaminedEvent>(OnBallisticExamine);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, GetVerbsEvent<Verb>>(OnBallisticVerb);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, InteractUsingEvent>(OnBallisticInteractUsing);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, AfterInteractEvent>(OnBallisticAfterInteract);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, AmmoFillDoAfterEvent>(OnBallisticAmmoFillDoAfter);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, DelayedAmmoInsertDoAfterEvent>(OnBallisticDelayedAmmoInsertDoAfter);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, DelayedCycleDoAfterEvent>(OnBallisticDelayedCycleDoAfter);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, UseInHandEvent>(OnBallisticUse);

        SubscribeLocalEvent<BallisticAmmoSelfRefillerComponent, MapInitEvent>(OnBallisticRefillerMapInit);
        SubscribeLocalEvent<BallisticAmmoSelfRefillerComponent, EmpPulseEvent>(OnRefillerEmpPulsed);

        SubscribeLocalEvent<BallisticAmmoInteractLoaderComponent, AfterInteractEvent>(OnBallisticAmmoLoad);
    }

    /// <summary>
    ///  Use in hand. Calls ManualCycle to remove a round if component.Cycleable is true.
    ///  Separate because ManualCycle can also be called by the get-ballistic-cycle verb.
    ///  Using the weapon in hand is much faster than using the verb, but the verb still works even if that's overridden.
    /// </summary>
    private void OnBallisticUse(EntityUid uid, BallisticAmmoProviderComponent component, UseInHandEvent args)
    {
        if (args.Handled || !component.Cycleable)
            return;

        if (component.CycleDelay > 0)
            BallisticCycleDelayCheck(uid, component, args.User);
        else // If there's no custom CycleDelay on this component by default, immediately cycle.
            ManualCycle(uid, component, TransformSystem.GetMapCoordinates(uid), args.User);

        args.Handled = true;
    }

    private void BallisticCycleDelayCheck(EntityUid uid, BallisticAmmoProviderComponent component, EntityUid user)
    {
        // Handling cycleDelay and DoAfter here sinceboth UseInHandEvent and Verb need to do these checks.
        TimeSpan cycleDelayConverted;

        if (component.CycleDelay > 0)
        {
            Popup(
                Loc.GetString("gun-ballistic-cycle-delayed",
                    ("entity", uid)),
                    uid,
                    user);

            cycleDelayConverted = TimeSpan.FromSeconds(component.CycleDelay);
            _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, cycleDelayConverted, new DelayedCycleDoAfterEvent(), used: uid, target: uid, eventTarget: uid)
            {
                BreakOnMove = true,
                BreakOnDamage = false,
                NeedHand = true
            });
        }
        else
            ManualCycle(uid, component, TransformSystem.GetMapCoordinates(uid), user);
    }

    /// <summary>
    /// Interact with a BallisticAmmoProvider using something else in hand, usually to load it with loose cartridges or other ammo.
    /// Includes both magazines and some guns that take ammo directly, like shotguns and launchers.
    /// Uses InsertDelay instead of FillDelay, which defaults to 0. InsertDelay > 0 makes loading a DoAfter channel, even with Ammo components.
    /// If transferring from another BallisticAmmoProvider, OnBallisticAfterInteract takes precedence and uses FillDelay instead.
    /// </summary>
    private void OnBallisticInteractUsing(EntityUid uid, BallisticAmmoProviderComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (TryAmmoInsert(uid, component, args.Used, args.User, args.Target, component.InsertDelay))
            args.Handled = true;
    }

    public bool TryAmmoInsert(EntityUid uid, BallisticAmmoProviderComponent component, EntityUid ammo, EntityUid loader, EntityUid weapon, double insertDelay)
    {
        if (_whitelistSystem.IsWhitelistFailOrNull(component.Whitelist, ammo))
            return false;

        //Prevent primed grenades or other primed ordanance from being loaded into weapons.
        if (HasComp<ActiveTimerTriggerComponent>(ammo))
        {
            Popup(
                Loc.GetString("gun-ballistic-transfer-primed",
                    ("ammoEntity", ammo)),
                uid,
                loader);

            return false;
        }

        if (GetBallisticShots(component) >= component.Capacity)
            return false;

        //RMC14
        if (TryComp(ammo, out ExpendableLightComponent? light))
        {
            if (light.CurrentState != ExpendableLightState.BrandNew)
                return false;
        }

        TimeSpan insertDelayConverted;

        if (insertDelay > 0)
        {
            insertDelayConverted = TimeSpan.FromSeconds(insertDelay);
            _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, loader, insertDelayConverted, new DelayedAmmoInsertDoAfterEvent(), used: ammo, target: weapon, eventTarget: uid)
            {
                BreakOnMove = true,
                BreakOnDamage = false,
                NeedHand = true
            });
        }
        else // If there's no custom InsertDelay on this component, immediately load the ammo in. Shotguns and mag-filling.
        {
            ManualLoad(uid, component, ammo, loader);
        }

        return true;
    }

    /// <summary>
    /// Interacting with a BallisticAmmoProvider with another one, to transfer ammo.
    /// Uses FillDelay, defaulting to 0.5s
    /// </summary>
    private void OnBallisticAfterInteract(EntityUid uid, BallisticAmmoProviderComponent component, AfterInteractEvent args)
    {
        if (args.Handled ||
            !component.MayTransfer ||
            args.Target == null ||
            args.Used == args.Target ||
            Deleted(args.Target) ||
            !TryComp<BallisticAmmoProviderComponent>(args.Target, out var targetComponent) ||
            targetComponent.Whitelist == null)
        {
            return;
        }

        // RMC14
        if (Containers.TryGetContainingContainer((args.Target.Value, null), out var container) &&
            container.Owner != args.User &&
            HasComp<StorageComponent>(container.Owner))
        {
            return;
        }

        args.Handled = true;

        // Continuous loading
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, component.FillDelay, new AmmoFillDoAfterEvent(), used: uid, target: args.Target, eventTarget: uid)
        {
            BreakOnMove = true,
            BreakOnDamage = false,
            NeedHand = true,
        });
    }

    private void OnBallisticAmmoFillDoAfter(EntityUid uid, BallisticAmmoProviderComponent component, AmmoFillDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (Deleted(args.Target) ||
            !TryComp<BallisticAmmoProviderComponent>(args.Target, out var target) ||
            target.Whitelist == null ||
            args.Cancelled)
        {
            Popup(
                Loc.GetString("gun-ballistic-transfer-cancelled",
                    ("entity", uid)),
                uid,
                args.User);
            return;
        }

        if (target.Entities.Count + target.UnspawnedCount == target.Capacity)
        {
            PopupSystem.PopupEntity(
                Loc.GetString("gun-ballistic-transfer-target-full", ("entity", args.Target.Value)),
                args.Target.Value,
                args.User);
            return;
        }

        if (component.Entities.Count + component.UnspawnedCount == 0)
        {
            PopupSystem.PopupEntity(
                Loc.GetString("gun-ballistic-transfer-empty", ("entity", uid)),
                uid,
                args.User);
            return;
        }
        // Simulates using a single ammo entity on the other BAP, loading it in.
        void SimulateInsertAmmo(EntityUid ammo, EntityUid ammoProvider, EntityCoordinates coordinates)
        {
            // We call SharedInteractionSystem to raise contact events. Checks are already done by this point.
            _interaction.InteractUsing(args.User, ammo, ammoProvider, coordinates, checkCanInteract: false, checkCanUse: false);
        }

        List<(EntityUid? Entity, IShootable Shootable)> ammo = new();
        var evTakeAmmo = new TakeAmmoEvent(Math.Clamp(target.Capacity - target.Count, 0, 20), ammo, Transform(uid).Coordinates, args.User); // RMC14
        RaiseLocalEvent(uid, evTakeAmmo);

        foreach (var (ent, _) in ammo)
        {
            if (ent == null)
                continue;

            if (_whitelistSystem.IsWhitelistFail(target.Whitelist, ent.Value))
            {
                PopupSystem.PopupEntity(
                    Loc.GetString("gun-ballistic-transfer-invalid",
                        ("ammoEntity", ent.Value),
                        ("targetEntity", args.Target.Value)),
                    uid,
                    args.User);

                SimulateInsertAmmo(ent.Value, uid, Transform(uid).Coordinates);
            }
            else
            {
                // play sound to be cool
                Audio.PlayPredicted(component.SoundInsert, uid, args.User);
                SimulateInsertAmmo(ent.Value, args.Target.Value, Transform(args.Target.Value).Coordinates);
            }

            if (IsClientSide(ent.Value))
                Del(ent.Value);
        }

        // repeat if there is more space in the target and more ammo to fill
        var moreSpace = target.Entities.Count + target.UnspawnedCount < target.Capacity;
        var moreAmmo = component.Entities.Count + component.UnspawnedCount > 0;
        args.Repeat = moreSpace && moreAmmo;

        // Delete the source BAP if it has the flag and is empty after trying to load. Maybe useful for shell handfuls.
        if (component.DeleteWhenEmpty && (component.Entities.Count == 0))
            PredictedDel(uid);

    }

    private void OnBallisticDelayedAmmoInsertDoAfter(EntityUid uid, BallisticAmmoProviderComponent component, DelayedAmmoInsertDoAfterEvent args)
    {
        // Check the DoAfter wasn't cancelled and nothing's missing.
        if (Deleted(args.Target) ||
            !TryComp<BallisticAmmoProviderComponent>(args.Target, out var target) ||
            target.Whitelist == null ||
            args.Cancelled)
        {
            Popup(
                Loc.GetString("gun-ballistic-transfer-cancelled",
                    ("entity", uid)),
                uid,
                args.User);
            return;
        }
        // Check if full.
        if (target.Entities.Count + target.UnspawnedCount == target.Capacity)
        {
            Popup(
                Loc.GetString("gun-ballistic-transfer-target-full",
                    ("entity", args.Target)),
                args.Target,
                args.User);
            return;
        }

        if (args.Used is not null)
        {
            ManualLoad(uid, component, args.Used.Value, args.User);
            args.Handled = true;
            return;
        }
        args.Handled = false;
    }

    private void ManualLoad(EntityUid uid, BallisticAmmoProviderComponent component, EntityUid used, EntityUid user)
    {
        if (TryComp(used, out StackComponent? stack))
        {
            var coordinates = TransformSystem.GetMoverCoordinates(used);
            var split = _rmcStack.Split((used, stack), 1, coordinates);
            if (split != null)
                used = split.Value;

            if (CompOrNull<CartridgeAmmoComponent>(used)?.SoundInsert is { } sound)
                Audio.PlayPredicted(sound, uid, user);

            UpdateAmmoCount(uid, artificialIncrease: 1);

            if (_netManager.IsClient)
                return;
        }

        // Reused function moved here.
        component.Entities.Add(used);
        Containers.Insert(used, component.Container);
        Dirty(uid, component);
        // Not predicted so
        Audio.PlayPredicted(component.SoundInsert, uid, user);
        UpdateBallisticAppearance(uid, component);
        UpdateAmmoCount(uid);
        DirtyField(uid, component, nameof(BallisticAmmoProviderComponent.Entities));
        return;
    }

    private void OnBallisticDelayedCycleDoAfter(EntityUid uid, BallisticAmmoProviderComponent component, DelayedCycleDoAfterEvent args)
    {
        // Check the DoAfter wasn't interrupted and the target BAP still exists.
        if (Deleted(uid) ||
            args.Cancelled)
        {
            Popup(
                Loc.GetString("gun-ballistic-cycle-delayed-cancelled",
                    ("entity", uid)),
                uid,
                args.User);
            return;
        }
        // Check if empty.
        if (component.Entities.Count + component.UnspawnedCount == 0)
        {
            Popup(
                Loc.GetString("gun-ballistic-cycle-delayed-empty",
                    ("entity", uid)),
                uid,
                args.User);
            return;
        }

        ManualCycle(uid, component, TransformSystem.GetMapCoordinates(uid), args.User);

        args.Handled = true;
    }

    private void OnBallisticVerb(EntityUid uid, BallisticAmmoProviderComponent component, GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null || !component.Cycleable)
            return;

        if (component.Cycleable)
        {
            args.Verbs.Add(new Verb()
            {
                Text = Loc.GetString("gun-ballistic-cycle"),
                Disabled = (GetBallisticShots(component) == 0),
                Act = () => BallisticCycleDelayCheck(uid, component, args.User),
            });

        }
    }

    private void OnBallisticExamine(Entity<BallisticAmmoProviderComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("gun-magazine-examine", ("color", AmmoExamineColor), ("count", GetBallisticShots(ent.Comp))));
    }

    private void ManualCycle(Entity<BallisticAmmoProviderComponent> ent, MapCoordinates coordinates, EntityUid? user = null, GunComponent? gunComp = null)
    {
        if (!ent.Comp.Cycleable)
            return;

        // Reset shotting for cycling
        if (Resolve(ent, ref gunComp, false) &&
            gunComp is { FireRateModified: > 0f } &&
            !Paused(ent))
        {
            gunComp.NextFire = Timing.CurTime + TimeSpan.FromSeconds(1 / gunComp.FireRateModified);
            DirtyField(ent, gunComp, nameof(GunComponent.NextFire));
        }

        Audio.PlayPredicted(ent.Comp.SoundRack, ent, user);

        var shots = GetBallisticShots(ent.Comp);
        Cycle(ent, coordinates);

        var text = Loc.GetString(shots == 0 ? "gun-ballistic-cycled-empty" : "gun-ballistic-cycled");

        PopupSystem.PopupEntity(text, ent, user);
        UpdateBallisticAppearance(ent);
        UpdateAmmoCount(ent);
    }

    protected abstract void Cycle(Entity<BallisticAmmoProviderComponent> ent, MapCoordinates coordinates);

    private void OnBallisticInit(Entity<BallisticAmmoProviderComponent> ent, ref ComponentInit args)
    {
        ent.Comp.Container = Containers.EnsureContainer<Container>(ent, "ballistic-ammo");
        // TODO: This is called twice though we need to support loading appearance data (and we need to call it on MapInit
        // to ensure it's correct).
        UpdateBallisticAppearance(ent);
    }

    private void OnBallisticMapInit(Entity<BallisticAmmoProviderComponent> ent, ref MapInitEvent args)
    {
        // TODO this should be part of the prototype, not set on map init.
        // Alternatively, just track spawned count, instead of unspawned count.
        if (ent.Comp.Proto != null)
        {
            ent.Comp.UnspawnedCount = Math.Max(0, ent.Comp.Capacity - ent.Comp.Container.ContainedEntities.Count);
            UpdateBallisticAppearance(ent);
            DirtyField(ent.AsNullable(), nameof(BallisticAmmoProviderComponent.UnspawnedCount));
        }
    }

    protected int GetBallisticShots(BallisticAmmoProviderComponent component)
    {
        return component.Entities.Count + component.UnspawnedCount;
    }

    private void OnBallisticTakeAmmo(Entity<BallisticAmmoProviderComponent> ent, ref TakeAmmoEvent args)
    {
        for (var i = 0; i < args.Shots; i++)
        {
            EntityUid? ammoEntity = null;
            if (ent.Comp.Entities.Count > 0)
            {
                var existingEnt = ent.Comp.Entities[^1];
                ent.Comp.Entities.RemoveAt(ent.Comp.Entities.Count - 1);
                DirtyField(ent.AsNullable(), nameof(BallisticAmmoProviderComponent.Entities));
                Containers.Remove(existingEnt, ent.Comp.Container);
                ammoEntity = existingEnt;
            }
            else if (ent.Comp.UnspawnedCount > 0)
            {
                ent.Comp.UnspawnedCount--;
                DirtyField(ent.AsNullable(), nameof(BallisticAmmoProviderComponent.UnspawnedCount));
                ammoEntity = Spawn(ent.Comp.Proto, args.Coordinates);
            }

            if (ammoEntity is not { } ammoEnt)
                continue;

            args.Ammo.Add((ammoEnt, EnsureShootable(ammoEnt)));
            if (TryComp<BallisticAmmoSelfRefillerComponent>(ent, out var refiller))
            {
                PauseSelfRefill((ent, refiller));
            }
        }

        UpdateBallisticAppearance(ent);
    }

    private void OnBallisticAmmoCount(Entity<BallisticAmmoProviderComponent> ent, ref GetAmmoCountEvent args)
    {
        args.Count = GetBallisticShots(ent.Comp);
        args.Capacity = ent.Comp.Capacity;
    }

    /// <summary>
    /// Causes <paramref name="entity"/> to pause its refilling for either at least <paramref name="overridePauseDuration"/>
    /// (if not null) or the entity's <see cref="BallisticAmmoSelfRefillerComponent.AutoRefillPauseDuration"/>. If the
    /// entity's next refill would occur after the pause duration, this function has no effect.
    /// </summary>
    public void PauseSelfRefill(
        Entity<BallisticAmmoSelfRefillerComponent> entity,
        TimeSpan? overridePauseDuration = null
    )
    {
        if (overridePauseDuration == null && !entity.Comp.FiringPausesAutoRefill)
            return;

        var nextRefillByPause = Timing.CurTime + (overridePauseDuration ?? entity.Comp.AutoRefillPauseDuration);
        if (nextRefillByPause > entity.Comp.NextAutoRefill)
        {
            entity.Comp.NextAutoRefill = nextRefillByPause;
            DirtyField(entity.AsNullable(), nameof(BallisticAmmoSelfRefillerComponent.NextAutoRefill));
        }
    }

    /// <summary>
    /// Returns true if the given <paramref name="entity"/>'s ballistic ammunition is full, false otherwise.
    /// </summary>
    public bool IsFull(Entity<BallisticAmmoProviderComponent> entity)
    {
        return GetBallisticShots(entity.Comp) >= entity.Comp.Capacity;
    }

    /// <summary>
    /// Returns whether or not <paramref name="inserted"/> can be inserted into <paramref name="entity"/>, based on
    /// available space and whitelists.
    /// </summary>
    public bool CanInsertBallistic(Entity<BallisticAmmoProviderComponent> entity, EntityUid inserted)
    {
        return !_whitelistSystem.IsWhitelistFailOrNull(entity.Comp.Whitelist, inserted) &&
               !IsFull(entity);
    }

    /// <summary>
    /// Attempts to insert <paramref name="inserted"/> into <paramref name="entity"/> as ammunition. Returns true on
    /// success, false otherwise.
    /// </summary>
    public bool TryBallisticInsert(
        Entity<BallisticAmmoProviderComponent> entity,
        EntityUid inserted,
        EntityUid? user,
        bool suppressInsertionSound = false
    )
    {
        inserted = _stack.GetOne(inserted);
        var ammoEv = new BeforeAmmoLoadedEvent();
        RaiseLocalEvent(inserted, ref ammoEv);

        if (!ammoEv.CanLoad)
            return false;

        var ammo = ammoEv.AmmoOverride ?? inserted;

        if (!CanInsertBallistic(entity, ammo))
            return false;

        entity.Comp.Entities.Add(ammo);
        Containers.Insert(ammo, entity.Comp.Container);
        if (!suppressInsertionSound)
        {
            Audio.PlayPredicted(entity.Comp.SoundInsert, entity, user);
        }

        UpdateBallisticAppearance(entity);
        UpdateAmmoCount(entity);
        DirtyField(entity.AsNullable(), nameof(BallisticAmmoProviderComponent.Entities));

        return true;
    }

    public void UpdateBallisticAppearance(Entity<BallisticAmmoProviderComponent> ent)
    {
        Appearance.SetData(ent, AmmoVisuals.AmmoCount, GetBallisticShots(ent.Comp));
        Appearance.SetData(ent, AmmoVisuals.AmmoMax, ent.Comp.Capacity);
    }

    public void SetBallisticUnspawned(Entity<BallisticAmmoProviderComponent> entity, int count)
    {
        if (entity.Comp.UnspawnedCount == count)
            return;

        entity.Comp.UnspawnedCount = count;
        UpdateBallisticAppearance(entity);
        UpdateAmmoCount(entity.Owner);
        Dirty(entity);
    }

    private void OnRefillerEmpPulsed(Entity<BallisticAmmoSelfRefillerComponent> entity, ref EmpPulseEvent args)
    {
        if (!entity.Comp.AffectedByEmp)
            return;

        PauseSelfRefill(entity, args.Duration);
    }

    private void OnBallisticAmmoLoad(Entity<BallisticAmmoInteractLoaderComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target == null)
            return;

        if (!TryComp<BallisticAmmoProviderComponent>(ent, out var ballisticAmmoProviderComp))
            return;

        if (TryBallisticInsert(
                (ent, ballisticAmmoProviderComp),
                args.Target.Value,
                args.User))
            args.Handled = true;
    }

    private void UpdateBallistic(float frameTime)
    {
        var query = EntityQueryEnumerator<BallisticAmmoSelfRefillerComponent, BallisticAmmoProviderComponent>();
        while (query.MoveNext(out var uid, out var refiller, out var ammo))
        {
            BallisticSelfRefillerUpdate((uid, ammo, refiller));
        }
    }

    private void BallisticSelfRefillerUpdate(
        Entity<BallisticAmmoProviderComponent, BallisticAmmoSelfRefillerComponent> entity
    )
    {
        var ammo = entity.Comp1;
        var refiller = entity.Comp2;
        if (Timing.CurTime < refiller.NextAutoRefill)
            return;

        refiller.NextAutoRefill += refiller.AutoRefillRate;
        DirtyField(entity, refiller, nameof(BallisticAmmoSelfRefillerComponent.NextAutoRefill));

        if (!refiller.AutoRefill || IsFull(entity))
            return;

        if (refiller.AmmoProto is not { } refillerAmmoProto)
        {
            // No ammo proto on the refiller, so just increment the unspawned count on the provider
            // if it has an ammo proto.
            if (ammo.Proto is null)
            {
                Log.Error(
                    $"Neither of {entity}'s {nameof(BallisticAmmoSelfRefillerComponent)}'s or {nameof(BallisticAmmoProviderComponent)}'s ammunition protos is specified. This is a configuration error as it means {nameof(BallisticAmmoSelfRefillerComponent)} cannot do anything.");
                return;
            }

            SetBallisticUnspawned(entity, ammo.UnspawnedCount + 1);
        }
        else if (ammo.Proto == refillerAmmoProto)
        {
            // The ammo proto on the refiller and the provider match. Add an unspawned ammo.
            SetBallisticUnspawned(entity, ammo.UnspawnedCount + 1);
        }
        else
        {
            // Can't use unspawned ammo, so spawn an entity and try to insert it.
            var ammoEntity = PredictedSpawnAttachedTo(refiller.AmmoProto, Transform(entity).Coordinates);
            var insertSucceeded = TryBallisticInsert(entity, ammoEntity, null, suppressInsertionSound: true);
            if (!insertSucceeded)
            {
                PredictedQueueDel(ammoEntity);
                Log.Error(
                    $"Failed to insert ammo {ammoEntity} into non-full {entity}. This is a configuration error. Is the {nameof(BallisticAmmoSelfRefillerComponent)}'s {nameof(BallisticAmmoSelfRefillerComponent.AmmoProto)} incorrect for the {nameof(BallisticAmmoProviderComponent)}'s {nameof(BallisticAmmoProviderComponent.Whitelist)}?");
            }
        }
    }
}

/// <summary>
/// DoAfter event for filling one ballistic ammo provider from another.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class AmmoFillDoAfterEvent : SimpleDoAfterEvent
{
}
