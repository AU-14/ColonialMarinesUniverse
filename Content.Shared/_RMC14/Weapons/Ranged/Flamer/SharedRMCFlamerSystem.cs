using System.Linq; // CMU14
using System.Diagnostics.CodeAnalysis;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared._RMC14.Fluids;
using Content.Shared._RMC14.Line;
using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Weapons.Common;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Actions;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Events; // CMU14
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Temperature;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._RMC14.Weapons.Ranged.Flamer;

public abstract partial class SharedRMCFlamerSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _action = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private LineSystem _line = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedRMCFlammableSystem _rmcFlammable = default!;
    [Dependency] private SharedRMCSpraySystem _rmcSpray = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SolutionTransferSystem _solutionTransfer = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private RMCMapSystem _rmcMap = default!;
    [Dependency] private RMCReagentSystem _reagent = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;
    [Dependency] private Content.Shared.CMU14.Chemistry.Fuel.CMUFuelProfileSystem _fuelProfile = default!; // CMU14
    [Dependency] private Content.Shared.CMU14.Weapons.CMUFlameConeSystem _flameCone = default!; // CMU14

    public override void Initialize()
    {
        SubscribeLocalEvent<RMCFlamerAmmoProviderComponent, MapInitEvent>(OnMapInit, after: [typeof(SharedSolutionContainerSystem)]);
        SubscribeLocalEvent<RMCFlamerAmmoProviderComponent, TakeAmmoEvent>(OnTakeAmmo);
        SubscribeLocalEvent<RMCFlamerAmmoProviderComponent, GetAmmoCountEvent>(OnGetAmmoCount);
        SubscribeLocalEvent<RMCFlamerAmmoProviderComponent, EntInsertedIntoContainerMessage>(OnInsertedIntoContainer);
        SubscribeLocalEvent<RMCFlamerAmmoProviderComponent, EntRemovedFromContainerMessage>(OnRemovedFromContainer);
        SubscribeLocalEvent<RMCFlamerAmmoProviderComponent, AttemptShootEvent>(OnAttemptShoot);

        SubscribeLocalEvent<RMCFlamerTankComponent, BeforeRangedInteractEvent>(OnFlamerTankBeforeRangedInteract);
        SubscribeLocalEvent<RMCFlamerTankComponent, GetVerbsEvent<ExamineVerb>>(OnFlamerTankVerbExamine);
        SubscribeLocalEvent<RMCFlamerTankComponent, TargetBeforeInjectEvent>(OnFlamerTankBeforeInject); // CMU14

        SubscribeLocalEvent<RMCSprayAmmoProviderComponent, TakeAmmoEvent>(OnSprayTakeAmmo);
        SubscribeLocalEvent<RMCSprayAmmoProviderComponent, GetAmmoCountEvent>(OnSprayGetAmmoCount);

        SubscribeLocalEvent<RMCIgniterComponent, MapInitEvent>(OnIgniterMapInit, after: [typeof(SharedSolutionContainerSystem)]);
        SubscribeLocalEvent<RMCIgniterComponent, UniqueActionEvent>(OnIgniterUniqueAction);
        SubscribeLocalEvent<RMCIgniterComponent, IsHotEvent>(OnIgniterToggle);
        SubscribeLocalEvent<RMCIgniterComponent, AttemptShootEvent>(OnIgniterAttemptShoot);
        SubscribeLocalEvent<RMCIgniterComponent, ExaminedEvent>(OnIgniterUniqueActionExamine, before: [typeof(SharedGunSystem)]);

        SubscribeLocalEvent<RMCBroilerComponent, GetItemActionsEvent>(OnBroilerGetItemActions);
        SubscribeLocalEvent<RMCBroilerComponent, RMCBroilerActionEvent>(OnBroilerAction);

        SubscribeLocalEvent<RMCCanUseBroilerComponent, UniqueActionEvent>(OnBroilerUniqueAction);
        SubscribeLocalEvent<RMCCanUseBroilerComponent, ExaminedEvent>(OnBroilerUniqueActionExamine, before: [typeof(SharedGunSystem)]);
    }

    private void OnMapInit(Entity<RMCFlamerAmmoProviderComponent> ent, ref MapInitEvent args)
    {
        UpdateAppearance(ent);
    }

    private void OnTakeAmmo(Entity<RMCFlamerAmmoProviderComponent> ent, ref TakeAmmoEvent args)
    {
        args.Ammo.Add((ent, ent.Comp));
    }

    private void OnGetAmmoCount(Entity<RMCFlamerAmmoProviderComponent> ent, ref GetAmmoCountEvent args)
    {
        if (!TryGetTankSolution(ent, out var solutionEnt, out var _))
            return;

        var solution = solutionEnt.Value.Comp.Solution;
        args.Count = solution.Volume.Int();
        args.Capacity = solution.MaxVolume.Int();
    }

    private void OnInsertedIntoContainer(Entity<RMCFlamerAmmoProviderComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.ContainerId)
            return;

        UpdateAppearance(ent);
    }

    private void OnRemovedFromContainer(Entity<RMCFlamerAmmoProviderComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.ContainerId)
            return;

        UpdateAppearance(ent);
    }

    private void OnAttemptShoot(Entity<RMCFlamerAmmoProviderComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled)
            return;

        if (args.ToCoordinates is not { } toCoordinates ||
            CanShootFlamer(ent, args.FromCoordinates, toCoordinates, out _, out var solution, out _, out _))
        {
            return;
        }

        args.Cancelled = true;
        args.ResetCooldown = true;

        var time = _timing.CurTime;
        if (time < ent.Comp.CantShootPopupLast + ent.Comp.CantShootPopupCooldown)
            return;

        ent.Comp.CantShootPopupLast = time;
        Dirty(ent);

        _audio.PlayPredicted(ent.Comp.DryFireSound, ent, args.User);

        if (solution is not { } sol || sol.Comp.Solution.Volume < ent.Comp.CostPer)
        {
            args.Message = Loc.GetString("rmc-flamer-empty");
            return;
        }

        args.Message = Loc.GetString("rmc-flamer-too-close");
    }

    private void OnFlamerTankBeforeRangedInteract(Entity<RMCFlamerTankComponent> tank, ref BeforeRangedInteractEvent args)
    {
        if (!args.CanReach)
            return;

        if (!HasComp<RMCFlamerAmmoProviderComponent>(tank))
        {
            RefillTank(tank, ref args);
            return;
        }

        if (args.Target is not { } target)
            return;

        if (!_solution.TryGetSolution(tank.Owner, tank.Comp.SolutionId, out var tankSolutionEnt, out _))
            return;

        Entity<SolutionComponent> targetSolutionEnt;
        if (_solution.TryGetDrainableSolution(target, out var drainable, out _))
        {
            targetSolutionEnt = drainable.Value;
        }
        else if (TryComp(target, out RMCFlamerTankComponent? targetTank) &&
                 _solution.TryGetSolution(target, targetTank.SolutionId, out var targetTankSolution))
        {
            targetSolutionEnt = targetTankSolution.Value;
        }
        else if (TryComp(target, out RMCFlamerBackpackComponent? backpack) &&
                 _solution.TryGetSolution(target, backpack.SolutionId, out var backpackSolution))
        {
            targetSolutionEnt = backpackSolution.Value;
        }
        else if (HasComp<ReagentTankComponent>(target) &&
                 _solution.TryGetDrainableSolution(target, out var reagentTankSolutionEnt, out _))
        {
            targetSolutionEnt = reagentTankSolutionEnt.Value;
        }
        else
        {
            return;
        }

        args.Handled = true;
        Transfer(target, targetSolutionEnt, tank, tankSolutionEnt.Value, args.User);
    }

    private void OnFlamerTankVerbExamine(Entity<RMCFlamerTankComponent> tank, ref GetVerbsEvent<ExamineVerb> args)
    {
        var user = args.User;

        if (!args.CanInteract || !args.CanAccess || HasComp<XenoComponent>(user))
            return;

        var msg = new FormattedMessage();
        // CMU14 start - tanks hold blends, so show what the loaded fuel will actually fire before the tank caps
        AddLoadedFuelExamine(tank, msg);
        msg.PushNewline();
        // CMU14 end
        List<int> values = new([tank.Comp.MaxIntensity, tank.Comp.MaxDuration, tank.Comp.MaxRange]);
        for (var i = 0; i < values.Count; i++)
        {
            msg.AddMarkupPermissive(Loc.GetString("rmc-flamer-tank-examine-line-" + i, ("value", values[i])));

            if (i + 1 != values.Count)
                msg.PushNewline();
        }

        _examine.AddDetailedExamineVerb(args,
            tank,
            msg,
            Loc.GetString("rmc-flamer-tank-examine-short"),
            tank.Comp.ExamineIcon,
            Loc.GetString("rmc-flamer-tank-examine")
        );
    }

    // CMU14 start
    /// <summary>
    /// What the tank's current contents will fire: the blended <paramref name="raw"/> profile and the
    /// <paramref name="effective"/> values after this tank's intensity, duration and range caps, using the same
    /// rules as <see cref="CanShootFlamer"/> and the chain update. False when the tank is empty or will not burn.
    /// </summary>
    public bool TryGetEffectiveFuel(Entity<RMCFlamerTankComponent> tank,
        out Content.Shared.CMU14.Chemistry.Fuel.CMUFuelProfile raw,
        out Content.Shared.CMU14.Chemistry.Fuel.CMUFuelProfile effective)
    {
        raw = default;
        effective = default;
        if (!_solution.TryGetSolution(tank.Owner, tank.Comp.SolutionId, out _, out var solution) ||
            !_fuelProfile.TryCompute(solution, out raw) ||
            !_fuelProfile.TryResolveFire(solution, Content.Shared.CMU14.Chemistry.Fuel.CMUFireSource.Flamer, out var spec, tank))
        {
            return false;
        }

        // FLOW-22: the same spec the flamer fires, so non-specialist tanks report the 39 intensity cap.
        effective = raw with
        {
            Intensity = spec.Intensity,
            Duration = spec.Duration,
            Radius = spec.Radius,
            FireEntity = spec.BehaviourEntity,
            FirePenetrating = spec.PenetrationLevel >= 1,
        };
        return true;
    }

    /// <summary>FLOW-22: the fire spec the tank's current contents fire with, after its caps.</summary>
    public bool TryGetTankFireSpec(Entity<RMCFlamerTankComponent> tank, out Content.Shared.CMU14.Chemistry.Fuel.CMUFireSpec spec)
    {
        spec = default;
        return _solution.TryGetSolution(tank.Owner, tank.Comp.SolutionId, out _, out var solution) &&
               _fuelProfile.TryResolveFire(solution, Content.Shared.CMU14.Chemistry.Fuel.CMUFireSource.Flamer, out spec, tank);
    }

    /// <summary>Blue fire: 40 max stacks instead of 20, reserved for specialist tanks.</summary>
    public static readonly EntProtoId BlueFire = "RMCTileFireBlue";

    /// <summary>
    /// The fire entity <paramref name="tank"/> actually fires for a fuel whose fire is <paramref name="fire"/>:
    /// blue fire falls back to the tank's <see cref="RMCFlamerTankComponent.FallbackFire"/> unless the tank
    /// allows it. FLOW-22: this only picks the behaviour entity; colour comes from the fire's intensity tier and
    /// non-specialist tanks are capped below the blue tier (CMUFuelProfileSystem.TryResolveFire).
    /// </summary>
    public EntProtoId GetTankFireEntity(Entity<RMCFlamerTankComponent> tank, EntProtoId fire)
    {
        if (tank.Comp.AllowBlueFire || !IsBlueFire(fire))
            return fire;

        return tank.Comp.FallbackFire;
    }

    private bool IsBlueFire(EntProtoId fire)
    {
        return _fuelProfile.IsBlueFire(fire);
    }

    private void AddLoadedFuelExamine(Entity<RMCFlamerTankComponent> tank, FormattedMessage msg)
    {
        msg.AddMarkupPermissive(Loc.GetString("cmu-flamer-tank-examine-fuel-header"));
        msg.PushNewline();
        if (!TryGetEffectiveFuel(tank, out var raw, out var effective))
        {
            var empty = !_solution.TryGetSolution(tank.Owner, tank.Comp.SolutionId, out _, out var solution) ||
                        solution.Volume <= FixedPoint2.Zero;
            msg.AddMarkupPermissive(Loc.GetString(empty ? "cmu-flamer-tank-examine-fuel-empty" : "cmu-flamer-tank-examine-fuel-inert"));
            msg.PushNewline();
            return;
        }

        AddFuelLine("cmu-flamer-tank-examine-fuel-intensity", effective.Intensity, raw.Intensity);
        AddFuelLine("cmu-flamer-tank-examine-fuel-duration", effective.Duration, raw.Duration);
        AddFuelLine("cmu-flamer-tank-examine-fuel-range", effective.Radius, tank.Comp.IgnoreReagentRange ? effective.Radius : raw.Radius);
        if (effective.FireSpread)
        {
            msg.AddMarkupPermissive(Loc.GetString("cmu-flamer-tank-examine-fuel-spread"));
            msg.PushNewline();
        }
        if (effective.FirePenetrating)
        {
            msg.AddMarkupPermissive(Loc.GetString("cmu-flamer-tank-examine-fuel-penetrating"));
            msg.PushNewline();
        }

        void AddFuelLine(string key, int value, int blend)
        {
            msg.AddMarkupPermissive(Loc.GetString(key, ("value", value)));
            if (blend > value)
                msg.AddMarkupPermissive(Loc.GetString("cmu-flamer-tank-examine-fuel-capped", ("blend", blend)));
            msg.PushNewline();
        }
    }
    // CMU14 end

    private void OnSprayTakeAmmo(Entity<RMCSprayAmmoProviderComponent> ent, ref TakeAmmoEvent args)
    {
        args.Ammo.Add((ent, ent.Comp));
    }

    private void OnSprayGetAmmoCount(Entity<RMCSprayAmmoProviderComponent> ent, ref GetAmmoCountEvent args)
    {
        if (!_solution.TryGetSolution(ent.Owner, ent.Comp.SolutionId, out var solutionEnt, out _))
            return;

        var solution = solutionEnt.Value.Comp.Solution;
        args.Count = solution.Volume.Int();
        args.Capacity = solution.MaxVolume.Int();
    }

    private void OnIgniterMapInit(Entity<RMCIgniterComponent> ent, ref MapInitEvent args)
    {
        _appearance.SetData(ent, RMCIgniterVisuals.Ignited, ent.Comp.Enabled);
    }

    private void OnIgniterUniqueAction(Entity<RMCIgniterComponent> ent, ref UniqueActionEvent args)
    {
        if (args.Handled || ent.Comp.Locked)
            return;

        args.Handled = true;
        ent.Comp.Enabled = !ent.Comp.Enabled;
        Dirty(ent);

        var sound = ent.Comp.Enabled
            ? ent.Comp.IgniteSound ?? ent.Comp.Sound
            : ent.Comp.ExtinguishSound ?? ent.Comp.Sound;
        _audio.PlayPredicted(sound, ent, args.UserUid);
        _appearance.SetData(ent, RMCIgniterVisuals.Ignited, ent.Comp.Enabled);
    }

    private void OnIgniterToggle(Entity<RMCIgniterComponent> ent, ref IsHotEvent args)
    {
        if (TryComp<AttachableHolderComponent>(ent, out var holder) &&
            holder.SupercedingAttachable != null)
        {
            args.IsHot = false;
            return;
        }

        args.IsHot = ent.Comp.Enabled;
    }

    protected virtual void OnIgniterAttemptShoot(Entity<RMCIgniterComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled)
            return;

        if (!ent.Comp.Enabled)
            args.Cancelled = true;
    }

    private void OnIgniterUniqueActionExamine(Entity<RMCIgniterComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Locked)
            return;

        args.PushMarkup(Loc.GetString(ent.Comp.ExamineText), 1);
    }

    private void UpdateAppearance(Entity<RMCFlamerAmmoProviderComponent> ent)
    {
        if (!TryComp(ent, out AppearanceComponent? appearance))
            return;

        var volume = FixedPoint2.Zero;
        var maxVolume = FixedPoint2.Zero;
        var tank = false;
        if (TryGetTankSolution(ent, out var solutionEnt, out var _, display: true))
        {
            var solution = solutionEnt.Value.Comp.Solution;
            volume = solution.Volume;
            maxVolume = solution.MaxVolume;
            tank = true;
        }

        _appearance.SetData(ent, AmmoVisuals.HasAmmo, volume > FixedPoint2.Zero, appearance);
        _appearance.SetData(ent, AmmoVisuals.AmmoCount, volume.Int(), appearance);
        _appearance.SetData(ent, AmmoVisuals.AmmoMax, maxVolume.Int(), appearance);
        _appearance.SetData(ent, AmmoVisuals.MagLoaded, tank, appearance);
        _appearance.SetData(ent, RMCFlamerVisualLayers.Strip, tank, appearance);
    }

    public void ShootFlamer(Entity<RMCFlamerAmmoProviderComponent> flamer,
        Entity<GunComponent> gun,
        EntityUid? user,
        EntityCoordinates fromCoordinates,
        EntityCoordinates toCoordinates)
    {
        if (!CanShootFlamer(flamer, fromCoordinates, toCoordinates, out var tiles, out var solution, out var spec, out var tank)) // CMU14
            return;

        if (!_zLevels.PlayPredictedDirectlyAcrossZ(gun.Comp.SoundGunshotModified, gun, user))
            _audio.PlayPredicted(gun.Comp.SoundGunshotModified, gun, user);

        //  333456
        // 1233456
        //  333456
        // CMU14: the cone's side tiles are free; fuel pays for the reach (one unit per distance step),
        // so widening the flame into a cone doesn't change how much a shot uses.
        var cost = tiles.Select(tile => tile.At).Distinct().Count();

        solution.Value.Comp.Solution.RemoveSolution(flamer.Comp.CostPer * cost);
        _solution.UpdateChemicals(solution.Value);

        if (_net.IsClient)
            return;

        var chain = Spawn();
        var chainComp = EnsureComp<RMCFlamerChainComponent>(chain);
        // CMU14 start - size the fire from the tank fire spec (FLOW-22: caps already applied)
        chainComp.Spawn = spec.BehaviourEntity;
        chainComp.Tiles = tiles;
        chainComp.Origin = fromCoordinates;
        chainComp.Intensity = spec.Intensity;
        chainComp.Duration = spec.Duration;
        chainComp.FirePenetrating = spec.PenetrationLevel >= 1;
        chainComp.PenetrationLevel = spec.PenetrationLevel;
        chainComp.SpreadDepth = spec.SpreadDepth;
        // CMU14 end
        chainComp.MaxIntensity = tank.Value.Comp.MaxIntensity;
        chainComp.MaxDuration = tank.Value.Comp.MaxDuration;

        Dirty(chain, chainComp);
    }

    public bool TryGetPreviewTiles(
        Entity<RMCFlamerAmmoProviderComponent> flamer,
        EntityCoordinates fromCoordinates,
        EntityCoordinates toCoordinates,
        [NotNullWhen(true)] out List<LineTile>? tiles)
    {
        return CanShootFlamer(flamer, fromCoordinates, toCoordinates, out tiles, out _, out _, out _);
    }

    public bool TryGetFuelColor(Entity<RMCFlamerAmmoProviderComponent> flamer, out Color color)
    {
        color = default;
        if (!TryGetTankSolution(flamer, out var solutionEnt, out _))
            return false;

        color = solutionEnt.Value.Comp.Solution.GetColor(_prototypes);
        return true;
    }

    private bool CanShootFlamer(
        Entity<RMCFlamerAmmoProviderComponent> flamer,
        EntityCoordinates fromCoordinates,
        EntityCoordinates toCoordinates,
        [NotNullWhen(true)] out List<LineTile>? tiles,
        [NotNullWhen(true)] out Entity<SolutionComponent>? solution,
        out Content.Shared.CMU14.Chemistry.Fuel.CMUFireSpec spec, // CMU14
        [NotNullWhen(true)] out Entity<RMCFlamerTankComponent>? tank)
    {
        tiles = null;
        spec = default; // CMU14
        if (!TryGetTankSolution(flamer, out solution, out tank))
            return false;

        var volume = solution.Value.Comp.Solution.Volume;
        if (volume < flamer.Comp.CostPer)
            return false;

        if (!fromCoordinates.TryDelta(EntityManager, _transform, toCoordinates, out var delta))
            return false;

        if (delta.IsLengthZero())
            return false;

        var normalized = -delta.Normalized();

        // to prevent hitting yourself
        fromCoordinates = fromCoordinates.Offset(normalized * 0.23f);

        // CMU14 start - the whole tank solution decides range, spread and fire type, not the first reagent.
        // FLOW-22: one fire spec (tank caps, 39 unless specialist) and a cone instead of a line or 3-wide band.
        if (!_fuelProfile.TryResolveFire(solution.Value.Comp.Solution, Content.Shared.CMU14.Chemistry.Fuel.CMUFireSource.Flamer, out spec, tank.Value))
            return false;

        var maxRange = spec.Radius;
        var affordable = (volume / flamer.Comp.CostPer).Int();
        var range = Math.Min(affordable, maxRange);
        if (delta.Length() > maxRange)
            toCoordinates = fromCoordinates.Offset(normalized * range);

        tiles = _line.DrawLine(fromCoordinates, toCoordinates, flamer.Comp.DelayPer, maxRange, out _, true);
        tiles = _flameCone.WidenToCone(fromCoordinates, tiles, spec.FireSpread, tank.Value.Comp.AllowBlueFire);

        // Fuel pays per distance step (see ShootFlamer), and the line is already cut to what the tank affords.
        // CMU14 end
        if (tiles.Count == 0)
        {
            tiles = null;
            return false;
        }

        return true;
    }

    public void ShootSpray(Entity<RMCSprayAmmoProviderComponent> spray,
        Entity<GunComponent> gun,
        EntityUid? user,
        EntityCoordinates fromCoordinates,
        EntityCoordinates toCoordinates)
    {
        if (user == null)
            return;

        _rmcSpray.Spray(spray, user.Value, _transform.ToMapCoordinates(toCoordinates), spray.Comp.HitUser);
    }

    /// <summary>
    /// Get the solution that will be used by the flamer
    /// </summary>
    /// <param name="flamer">The incinerator that is being used.</param>
    /// <param name="solutionEnt">The found solution.</param>
    /// <param name="tankEnt">The found tank.</param>
    /// <param name="display">Is this just being called to configure the sprite? It ignores the Broiler if true.</param>
    /// <returns>True if a solution has been found.</returns>
    private bool TryGetTankSolution(Entity<RMCFlamerAmmoProviderComponent> flamer, [NotNullWhen(true)] out Entity<SolutionComponent>? solutionEnt, [NotNullWhen(true)] out Entity<RMCFlamerTankComponent>? tankEnt, bool display = false)
    {
        solutionEnt = null;
        tankEnt = null;

        if (TryComp(flamer, out RMCFlamerTankComponent? tankComp))
        {
            tankEnt = (flamer, tankComp);
        }
        else if (_container.TryGetContainer(flamer, flamer.Comp.ContainerId, out var container) &&
                 container.ContainedEntities.TryFirstOrNull(out var tankId) &&
                 TryComp(tankId, out tankComp))
        {
            tankEnt = (tankId.Value, tankComp);
        }
        else if (!display && HasComp<RMCCanUseBroilerComponent>(flamer))
        {
            if (!_container.TryGetContainingContainer((flamer.Owner, null), out var holder))
                return false;

            var inventoryEnumerator = _inventory.GetSlotEnumerator(holder.Owner);
            while (inventoryEnumerator.MoveNext(out var slot))
            {
                if (!TryComp<RMCBroilerComponent>(slot.ContainedEntity, out var broiler))
                    continue;

                Entity<RMCBroilerComponent> broilerEnt = (slot.ContainedEntity.Value, broiler);
                var containers = BroilerListTanks(broilerEnt);
                if (containers.Count <= broiler.ActiveTank)
                    continue;

                var activeTankContainerName = containers[broiler.ActiveTank];
                if (!_container.TryGetContainer(broilerEnt, activeTankContainerName, out var activeTankContainer))
                    continue;

                if (!activeTankContainer.ContainedEntities.TryFirstOrNull(out tankId))
                    continue;

                if (!TryComp(tankId, out tankComp))
                    continue;

                tankEnt = (tankId.Value, tankComp);
                break;
            }
        }

        if (tankEnt is not { } tankValue)
            return false;

        return _solution.TryGetSolution(tankValue.Owner, tankValue.Comp.SolutionId, out solutionEnt, out _);
    }

    private void Transfer(EntityUid source,
        Entity<SolutionComponent> sourceSolutionEnt,
        Entity<RMCFlamerTankComponent> target,
        Entity<SolutionComponent> targetSolutionEnt,
        EntityUid user)
    {
        var tankSolution = targetSolutionEnt.Comp.Solution;
        var targetSolution = sourceSolutionEnt.Comp.Solution;
        // CMU14 start - one gate shared with beaker-on-tank pours (SharedRMCChemistrySystem)
        if (!CanAcceptPour(target, tankSolution, targetSolution, out var reason))
        {
            _popup.PopupEntity(reason, source, user);
            return;
        }
        // CMU14 end

        var transfer = _solutionTransfer.Transfer(
            user,
            source,
            sourceSolutionEnt,
            target,
            targetSolutionEnt,
            tankSolution.AvailableVolume
        );

        if (transfer > FixedPoint2.Zero)
            _popup.PopupEntity(Loc.GetString("rmc-flamer-refill", ("refilled", target)), source, user);
    }

    // CMU14 start
    /// <summary>
    /// Syringes inject into any refillable solution without raising a transfer attempt, so they get the same
    /// whitelist and fuel-profile gate as every other way of filling a tank.
    /// </summary>
    private void OnFlamerTankBeforeInject(Entity<RMCFlamerTankComponent> ent, ref TargetBeforeInjectEvent args)
    {
        if (args.Cancelled ||
            !TryComp<InjectorComponent>(args.UsedInjector, out var injector) ||
            !_solution.TryGetSolution(args.UsedInjector, injector.SolutionName, out _, out var injectorSolution) ||
            !_solution.TryGetSolution(ent.Owner, ent.Comp.SolutionId, out _, out var tankSolution))
        {
            return;
        }

        var amount = FixedPoint2.Min(injector.CurrentTransferAmount ?? injectorSolution.Volume, injectorSolution.Volume);
        var portion = injectorSolution.Clone();
        if (portion.Volume > amount)
            portion = portion.SplitSolution(amount);

        if (!CanAcceptPour(ent, tankSolution, portion, out var reason))
        {
            args.OverrideMessage = reason;
            args.Cancel();
        }
    }

    /// <summary>
    /// Whether pouring <paramref name="incoming"/> into a flamer tank is allowed: every reagent must pass the
    /// tank's reagent whitelist, and the blend formed by the portion that actually fits must still burn.
    /// Used by both pour directions (tank used on a container, and a container poured into the tank).
    /// </summary>
    public bool CanAcceptPour(Entity<RMCFlamerTankComponent> tank, Solution tankSolution, Solution incoming, out string reason)
    {
        reason = string.Empty;
        foreach (var content in incoming.Contents)
        {
            if (tank.Comp.ReagentWhitelist is { } whitelist && !whitelist.Contains(content.Reagent.Prototype))
            {
                reason = Loc.GetString("rmc-flamer-tank-not-whitelisted", ("tank", tank));
                return false;
            }
        }

        var moved = FixedPoint2.Min(incoming.Volume, tankSolution.AvailableVolume);
        if (moved <= FixedPoint2.Zero)
            return true; // nothing will move; let the transfer report a full tank

        var portion = incoming.Clone();
        if (portion.Volume > moved)
            portion = portion.SplitSolution(moved);

        if (_fuelProfile.TryComputeMixed(tankSolution, portion, out _))
            return true;

        reason = Loc.GetString("rmc-flamer-tank-not-potent-enough");
        return false;
    }
    // CMU14 end

    private void RefillTank(Entity<RMCFlamerTankComponent> tank, ref BeforeRangedInteractEvent args)
    {
        if (args.Target is not { } target)
            return;

        if (!_solution.TryGetSolution(tank.Owner, tank.Comp.SolutionId, out var tankSolutionEnt, out _))
            return;

        Entity<SolutionComponent> targetSolutionEnt;
        if (HasComp<ReagentTankComponent>(target) &&
            _solution.TryGetDrainableSolution(target, out var reagentTankSolutionEnt, out _))
        {
            targetSolutionEnt = reagentTankSolutionEnt.Value;
        }
        // CMU14 start - lab glassware (beakers, jugs) may top up a tank directly; Transfer still applies the
        // mixed-profile potency gate, so only pours whose resulting blend burns are accepted.
        else if (HasComp<FitsInDispenserComponent>(target) &&
                 _solution.TryGetDrainableSolution(target, out var beakerSolutionEnt, out _))
        {
            targetSolutionEnt = beakerSolutionEnt.Value;
        }
        // CMU14 end
        else
        {
            return;
        }

        args.Handled = true;
        Transfer(target, targetSolutionEnt, tank, tankSolutionEnt.Value, args.User);
    }

    private void OnBroilerGetItemActions(Entity<RMCBroilerComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.SlotFlags == null || (args.SlotFlags & ent.Comp.Slot) == 0)
            return;

        args.AddAction(ref ent.Comp.Action, ent.Comp.ActionId, ent);
        if (ent.Comp.Action is { } action)
        {
            var n = ent.Comp.ActiveTank + 1;
            _action.SetIcon(action, new SpriteSpecifier.Rsi(ent.Comp.NumberingResource, n.ToString()));
        }
    }

    private List<string> BroilerListTanks(Entity<RMCBroilerComponent> ent)
    {
        List<string> list = [];
        foreach (var container in _container.GetAllContainers(ent))
        {
            var name = container.ID;
            if (name.StartsWith(ent.Comp.ContainerPrefix))
                list.Add(name);
        }
        return list;
    }

    private void OnBroilerAction(Entity<RMCBroilerComponent> ent, ref RMCBroilerActionEvent args)
    {
        args.Handled = true;

        ent.Comp.ActiveTank = (ent.Comp.ActiveTank + 1) % BroilerListTanks(ent).Count;
        Dirty(ent);

        var n = ent.Comp.ActiveTank + 1;
        if (ent.Comp.Action is { } action)
        {
            _action.SetIcon(action, new SpriteSpecifier.Rsi(ent.Comp.NumberingResource, n.ToString()));
        }

        _popup.PopupEntity(Loc.GetString("rmc-broiler-switch-tank", ("n", n)), ent, args.Performer);
    }

    public void OnBroilerUniqueAction(Entity<RMCCanUseBroilerComponent> ent, ref UniqueActionEvent args)
    {
        if (args.Handled)
            return;

        var inventoryEnumerator = _inventory.GetSlotEnumerator(args.UserUid);
        while (inventoryEnumerator.MoveNext(out var slot))
        {
            if (!TryComp<RMCBroilerComponent>(slot.ContainedEntity, out var _))
                continue;

            args.Handled = true;
            var ev = new RMCBroilerActionEvent();
            ev.Performer = args.UserUid;
            RaiseLocalEvent(slot.ContainedEntity.Value, ev);
            break;
        }
    }

    public void OnBroilerUniqueActionExamine(Entity<RMCCanUseBroilerComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(ent.Comp.ExamineText), 1);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var chains = EntityQueryEnumerator<RMCFlamerChainComponent>();
        while (chains.MoveNext(out var uid, out var comp))
        {
            if (comp.Tiles.Count == 0)
            {
                QueueDel(uid);
                continue;
            }

            for (var i = 0; i < comp.Tiles.Count; i++)
            {
                var tile = comp.Tiles[i];
                if (time < tile.At)
                    continue;

                comp.Tiles.RemoveAt(i);
                if (!_line.CanReachTile(comp.Origin, _transform.ToCoordinates(tile.Coordinates), hitBlocker: true)) // CMU14
                {
                    i--;
                    continue;
                }

                if (!_zLevels.TryProjectToGround(_transform.ToCoordinates(tile.Coordinates), out var fireCoordinates))
                {
                    i--;
                    continue;
                }

                var fire = Spawn(comp.Spawn, fireCoordinates);

                // check for any fires on the same tile other than the one we just spawned, and delete them
                if (_rmcMap.HasAnchoredEntityEnumerator<TileFireComponent>(fireCoordinates, out var oldTileFire)
                        && oldTileFire.Owner.Id != fire.Id)
                    QueueDel(oldTileFire);

                // CMU14 start - prefer the blended profile stored on the chain. FLOW-22: applied as one fire spec
                // (intensity/duration, lifetime scaled around napalm UT, penetration, colour tier, max stacks).
                if (comp.Intensity > 0 && comp.Duration > 0)
                {
                    var spec = Content.Shared.CMU14.Chemistry.Fuel.CMUFuelProfileSystem.CreateSpec(
                        Math.Min(comp.MaxIntensity, comp.Intensity),
                        Math.Min(comp.MaxDuration, comp.Duration),
                        0,
                        comp.Spawn,
                        Math.Max(comp.PenetrationLevel, comp.FirePenetrating ? 1 : 0),
                        spreadDepth: comp.SpreadDepth);
                    _rmcFlammable.ApplyFireSpec(fire, spec);
                }
                else if (_reagent.TryIndex(comp.Reagent, out var reagent))
                // CMU14 end
                {
                    var intensity = Math.Min(comp.MaxIntensity, reagent.Intensity);
                    var duration = Math.Min(comp.MaxDuration, reagent.Duration);
                    _rmcFlammable.SetIntensityDuration(fire, intensity, duration);
                }
                break;
            }
        }
    }
}
