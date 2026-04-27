using Content.Server.DeviceNetwork.Components;
using Content.Server.DeviceNetwork.Systems;
using Content.Server.Explosion.Components;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Popups;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Mortar;
using Content.Shared._RMC14.Ordnance;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Explosion.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Ordnance;

public sealed class RMCChembombSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DeviceNetworkSystem _deviceNetwork = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly SharedRMCFlammableSystem _flammable = default!;
    [Dependency] private readonly IMapManager _map = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly RMCOrdnanceShrapnelSystem _shrapnel = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    private static readonly SoundSpecifier InsertSound = new SoundPathSpecifier("/Audio/_RMC14/Weapons/Guns/Reload/grenade_insert.ogg");
    private static readonly SoundSpecifier ArmSound = new SoundPathSpecifier("/Audio/_RMC14/Explosion/armbomb.ogg");
    private static readonly SoundSpecifier C4TimerBeepSound = new SoundPathSpecifier("/Audio/Machines/Nuke/general_beep.ogg");
    private static readonly ProtoId<ToolQualityPrototype> ScrewingQuality = "Screwing";

    public override void Initialize()
    {
        SubscribeLocalEvent<RMCChembombCasingComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<RMCChembombCasingComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<RMCChembombCasingComponent, TriggerEvent>(OnTrigger);
        SubscribeLocalEvent<RMCChembombCasingComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<RMCChembombCasingComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<RMCChembombCasingComponent, EntInsertedIntoContainerMessage>(OnItemInserted);
        SubscribeLocalEvent<RMCChembombCasingComponent, EntRemovedFromContainerMessage>(OnItemRemoved);
        SubscribeLocalEvent<RMCChembombCasingComponent, RMCCasingScrewDoAfterEvent>(OnScrewDoAfter);
        SubscribeLocalEvent<RMCChembombCasingComponent, MortarShellLandEvent>(OnMortarShellLand);
        SubscribeLocalEvent<RMCDemolitionsScannerComponent, AfterInteractEvent>(OnScannerAfterInteract);
    }

    private void OnMapInit(Entity<RMCChembombCasingComponent> ent, ref MapInitEvent args)
    {
        UpdateCasingVisuals(ent);
    }

    private void OnUseInHand(Entity<RMCChembombCasingComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || _net.IsClient)
            return;

        if (ent.Comp.Stage == RMCCasingAssemblyStage.Armed)
            return;

        _popup.PopupEntity(Loc.GetString("rmc-chembomb-not-armed"), ent, args.User, PopupType.SmallCaution);
        args.Handled = true;
    }

    private void OnScrewDoAfter(Entity<RMCChembombCasingComponent> ent, ref RMCCasingScrewDoAfterEvent args)
    {
        if (args.Cancelled || _net.IsClient)
            return;

        if (ent.Comp.Stage == RMCCasingAssemblyStage.Open)
        {
            ent.Comp.Stage = RMCCasingAssemblyStage.Armed;
            _itemSlots.SetLock(ent, "detonator", true);
            _popup.PopupEntity(Loc.GetString("rmc-chembomb-armed"), ent, args.User);
        }
        else if (ent.Comp.Stage == RMCCasingAssemblyStage.Armed)
        {
            ent.Comp.Stage = RMCCasingAssemblyStage.Open;
            _itemSlots.SetLock(ent, "detonator", false);
            _popup.PopupEntity(Loc.GetString("rmc-chembomb-unsealed"), ent, args.User);
        }

        _audio.PlayPredicted(ArmSound, ent, args.User);
        UpdateCasingVisuals(ent);
        Dirty(ent);
    }

    private void OnScannerAfterInteract(Entity<RMCDemolitionsScannerComponent> scanner, ref AfterInteractEvent args)
    {
        if (!args.CanReach || args.Target is not { } target)
            return;

        if (!TryComp<RMCChembombCasingComponent>(target, out var casing))
            return;

        if (_net.IsClient)
            return;

        args.Handled = true;
        var state = GetScannerState(target, casing);
        _ui.SetUiState(scanner.Owner, RMCDemolitionsScannerUiKey.Key, state);
        _ui.TryOpenUi(scanner.Owner, RMCDemolitionsScannerUiKey.Key, args.User);
    }

    private void OnTrigger(Entity<RMCChembombCasingComponent> ent, ref TriggerEvent args)
    {
        if (_net.IsClient)
            return;

        if (ent.Comp.Stage != RMCCasingAssemblyStage.Armed)
        {
            QueueDel(ent);
            args.Handled = true;
            return;
        }

        if (!_solution.TryGetSolution(ent.Owner, ent.Comp.ChemicalSolution, out _, out var solution))
        {
            QueueDel(ent);
            args.Handled = true;
            return;
        }

        var estimate = RMCOrdnanceYieldEstimator.Estimate(solution, _prototype, RMCOrdnanceYieldEstimator.FromCasing(ent.Comp));

        var coords = _transform.GetMoverCoordinates(ent.Owner);

        if (estimate.HasExplosion)
        {
            _explosion.QueueExplosion(
                _transform.GetMapCoordinates(ent.Owner),
                "RMC",
                estimate.TotalIntensity,
                estimate.IntensitySlope,
                estimate.MaxIntensity,
                ent.Owner);
        }

        if (estimate.HasFire)
        {
            var tile = coords.SnapToGrid(EntityManager, _map);
            _flammable.SpawnFireDiamond(
                estimate.FireEntity,
                tile,
                (int) estimate.FireRadius,
                (int) estimate.FireIntensity,
                (int) estimate.FireDuration);
        }

        if (estimate.HasShards)
        {
            var shardAngle = estimate.Shrapnel.UseCasingDirection
                ? Transform(ent.Owner).WorldRotation
                : Angle.Zero;

            _shrapnel.SpawnBurst(
                _transform.GetMapCoordinates(ent.Owner),
                estimate.Shrapnel.ProjectileProto,
                estimate.Shrapnel.Count,
                ent.Owner,
                shardAngle,
                estimate.Shrapnel.SpreadAngle,
                estimate.Shrapnel.ProjectileSpeed);
        }

        args.Handled = true;
        QueueDel(ent.Owner);
    }

    private void OnMortarShellLand(Entity<RMCChembombCasingComponent> ent, ref MortarShellLandEvent args)
    {
        if (_net.IsClient || ent.Comp.Stage != RMCCasingAssemblyStage.Armed)
            return;

        RaiseLocalEvent(ent.Owner, new TriggerEvent(ent.Owner));
    }

    private void OnInteractUsing(Entity<RMCChembombCasingComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || _net.IsClient)
            return;

        if (_tool.HasQuality(args.Used, ScrewingQuality))
        {
            if (ent.Comp.Stage == RMCCasingAssemblyStage.Open && !ent.Comp.HasActiveDetonator)
            {
                _popup.PopupEntity(Loc.GetString("rmc-chembomb-seal-no-detonator"), ent, args.User, PopupType.SmallCaution);
                args.Handled = true;
                return;
            }

            StartToolDoAfter<RMCCasingScrewDoAfterEvent>(ent, args.User, args.Used, 2f);
            args.Handled = true;
            return;
        }

        if (ent.Comp.Stage != RMCCasingAssemblyStage.Open)
            return;

        if (!HasComp<FitsInDispenserComponent>(args.Used))
            return;

        if (!_solution.TryGetSolution(ent.Owner, ent.Comp.ChemicalSolution, out var casingSoln, out var casingChem))
            return;

        var remaining = casingChem.AvailableVolume;
        if (remaining <= FixedPoint2.Zero)
        {
            _popup.PopupEntity(Loc.GetString("rmc-chembomb-full"), ent.Owner, args.User, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        if (!_solution.TryGetFitsInDispenser(args.Used, out var beakerSoln, out var beakerChem))
            return;

        if (beakerChem.Volume <= FixedPoint2.Zero)
        {
            _popup.PopupEntity(Loc.GetString("rmc-chembomb-beaker-empty"), ent.Owner, args.User, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        var toTransfer = FixedPoint2.Min(remaining, beakerChem.Volume);
        var transferred = _solution.SplitSolution(beakerSoln.Value, toTransfer);
        _solution.TryAddSolution(casingSoln.Value, transferred);

        _audio.PlayPredicted(InsertSound, ent.Owner, args.User);
        _popup.PopupEntity(
            Loc.GetString("rmc-chembomb-fill", ("amount", toTransfer), ("total", casingChem.Volume + toTransfer), ("max", ent.Comp.MaxVolume)),
            ent.Owner,
            args.User);

        args.Handled = true;
    }

    private void OnExamined(Entity<RMCChembombCasingComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using (args.PushGroup(nameof(RMCChembombCasingComponent)))
        {
            var currentVol = FixedPoint2.Zero;
            if (_solution.TryGetSolution(ent.Owner, ent.Comp.ChemicalSolution, out _, out var chem))
                currentVol = chem.Volume;

            args.PushMarkup(Loc.GetString("rmc-chembomb-examine-volume", ("current", currentVol), ("max", ent.Comp.MaxVolume)));
            args.PushMarkup(ent.Comp.HasActiveDetonator
                ? Loc.GetString("rmc-chembomb-examine-detonator")
                : Loc.GetString("rmc-chembomb-examine-no-detonator"));

            args.PushMarkup(ent.Comp.Stage switch
            {
                RMCCasingAssemblyStage.Sealed => Loc.GetString("rmc-chembomb-examine-sealed"),
                RMCCasingAssemblyStage.Armed => Loc.GetString("rmc-chembomb-examine-locked"),
                _ => Loc.GetString("rmc-chembomb-examine-open"),
            });
        }
    }

    private void OnItemInserted(Entity<RMCChembombCasingComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (TryComp<RMCDetonatorAssemblyComponent>(args.Entity, out var oldComp))
        {
            if (!oldComp.Ready)
                return;

            ent.Comp.HasActiveDetonator = true;
            _audio.PlayPredicted(InsertSound, ent.Owner, null);
            UpdateCasingVisuals(ent);
            Dirty(ent);
            return;
        }

        if (!TryComp<RMCOrdnanceAssemblyComponent>(args.Entity, out var assembly) || !assembly.IsLocked)
            return;

        ent.Comp.HasActiveDetonator = true;
        _audio.PlayPredicted(InsertSound, ent.Owner, null);
        UpdateCasingVisuals(ent);
        Dirty(ent);

        ClearAssemblyTriggerComponents(ent);

        switch (GetAssemblyKind(assembly))
        {
            case RMCOrdnanceAssemblyKind.DoubleIgniter:
            {
                var trigger = EnsureComp<OnUseTimerTriggerComponent>(ent);
                trigger.Delay = 1f;
                trigger.DelayOptions = null;
                trigger.BeepSound = ArmSound;
                trigger.DoPopup = false;
                trigger.InitialBeepDelay = 0f;
                trigger.BeepInterval = 99999f;
                break;
            }
            case RMCOrdnanceAssemblyKind.Timer:
            {
                var trigger = EnsureComp<OnUseTimerTriggerComponent>(ent);
                trigger.Delay = assembly.TimerDelay;
                trigger.DelayOptions = null;
                trigger.BeepSound = ArmSound;
                trigger.DoPopup = false;
                trigger.InitialBeepDelay = 0f;
                trigger.BeepInterval = 5f;

                if (IsC4PlasticCasing(ent))
                {
                    trigger.BeepSound = C4TimerBeepSound;
                    trigger.BeepInterval = 1f;
                    trigger.StartOnStick = true;
                    trigger.AllowToggleStartOnStick = true;
                }

                break;
            }
            case RMCOrdnanceAssemblyKind.Signaler:
            {
                EnsureComp<TriggerOnSignalComponent>(ent);
                var network = EnsureComp<DeviceNetworkComponent>(ent);
                _deviceNetwork.SetReceiveFrequency(ent, assembly.SignalFrequency, network);
                _deviceNetwork.SetTransmitFrequency(ent, assembly.SignalFrequency, network);
                break;
            }
            case RMCOrdnanceAssemblyKind.Proximity:
                if (TryComp<RMCMineCasingComponent>(ent, out var mine))
                {
                    mine.TriggerRange = assembly.ProximityRange;
                    Dirty(ent, mine);
                }
                break;
        }
    }

    private void OnItemRemoved(Entity<RMCChembombCasingComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (HasComp<RMCDetonatorAssemblyComponent>(args.Entity))
        {
            ent.Comp.HasActiveDetonator = false;
            UpdateCasingVisuals(ent);
            Dirty(ent);
            return;
        }

        if (!HasComp<RMCOrdnanceAssemblyComponent>(args.Entity))
            return;

        ClearAssemblyTriggerComponents(ent);
        ent.Comp.HasActiveDetonator = false;
        UpdateCasingVisuals(ent);
        Dirty(ent);
    }

    private void ClearAssemblyTriggerComponents(Entity<RMCChembombCasingComponent> ent)
    {
        RemCompDeferred<OnUseTimerTriggerComponent>(ent);
        RemCompDeferred<TriggerOnSignalComponent>(ent);
    }

    private void StartToolDoAfter<T>(Entity<RMCChembombCasingComponent> ent, EntityUid user, EntityUid tool, float delay)
        where T : SimpleDoAfterEvent, new()
    {
        var doAfterArgs = new DoAfterArgs(EntityManager, user, delay, new T(), ent, ent, tool)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnHandChange = true,
        };
        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void UpdateCasingVisuals(Entity<RMCChembombCasingComponent> ent)
    {
        var state = ent.Comp.Stage switch
        {
            RMCCasingAssemblyStage.Armed => RMCCasingVisualState.Armed,
            RMCCasingAssemblyStage.Sealed => RMCCasingVisualState.Sealed,
            RMCCasingAssemblyStage.Open when ent.Comp.HasActiveDetonator => RMCCasingVisualState.Loaded,
            _ => RMCCasingVisualState.Empty,
        };

        _appearance.SetData(ent, RMCCasingVisualKey.State, state);
    }

    private static RMCOrdnanceAssemblyKind GetAssemblyKind(RMCOrdnanceAssemblyComponent comp)
    {
        if (comp.LeftPartType == RMCOrdnancePartType.RMCOrdnanceTimer || comp.RightPartType == RMCOrdnancePartType.RMCOrdnanceTimer)
            return RMCOrdnanceAssemblyKind.Timer;

        if (comp.LeftPartType == RMCOrdnancePartType.RMCOrdnanceSignaler || comp.RightPartType == RMCOrdnancePartType.RMCOrdnanceSignaler)
            return RMCOrdnanceAssemblyKind.Signaler;

        if (comp.LeftPartType == RMCOrdnancePartType.RMCOrdnanceProximitySensor || comp.RightPartType == RMCOrdnancePartType.RMCOrdnanceProximitySensor)
            return RMCOrdnanceAssemblyKind.Proximity;

        return RMCOrdnanceAssemblyKind.DoubleIgniter;
    }

    private bool IsC4PlasticCasing(Entity<RMCChembombCasingComponent> ent)
    {
        return TryComp<MetaDataComponent>(ent, out var meta) &&
               meta.EntityPrototype?.ID == "RMCC4PlasticCasing";
    }

    private RMCDemolitionsScannerBuiState GetScannerState(EntityUid target, RMCChembombCasingComponent casing)
    {
        _solution.TryGetSolution(target, casing.ChemicalSolution, out _, out var solution);
        var estimate = RMCOrdnanceYieldEstimator.Estimate(solution, _prototype, RMCOrdnanceYieldEstimator.FromCasing(casing));

        return new RMCDemolitionsScannerBuiState(
            MetaData(target).EntityName,
            estimate.CurrentVolume,
            (float) casing.MaxVolume,
            casing.HasActiveDetonator,
            casing.Stage,
            estimate.HasExplosion,
            estimate.TotalIntensity,
            estimate.BlastFalloff,
            estimate.ApproxBlastRadius,
            estimate.HasFire,
            estimate.FireIntensity,
            estimate.FireRadius,
            estimate.FireDuration);
    }

    private enum RMCOrdnanceAssemblyKind : byte
    {
        DoubleIgniter,
        Timer,
        Signaler,
        Proximity,
    }
}
