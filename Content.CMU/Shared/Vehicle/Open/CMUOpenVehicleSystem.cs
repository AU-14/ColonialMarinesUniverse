using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Actions;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.CMU14.Vehicle.Open;

public sealed partial class CMUOpenVehicleSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private CMUVehicleFuelSystem _fuel = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedPointLightSystem _lights = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private CMUVehiclePaintSystem _paint = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private CMUVehicleSeatSystem _seats = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedToolSystem _tool = default!;

    public const string WindshieldSlot = "jeep-windshield";
    public const string JerryCanSlot = "jeep-jerrycan";
    public const string SpareSlot = "jeep-spare";
    public const string ShovelSlot = "jeep-shovel";
    public const string AxeSlot = "jeep-axe";
    public const string JackSlot = "jeep-jack";
    public const string ExtinguisherSlot = "jeep-extinguisher";

    // The jeep's hinged panels, by part id.
    public const string Hood = "hood";
    public const string DriverDoor = "door_driver";
    public const string PassengerDoor = "door_passenger";
    public const string Tailgate = "tailgate";

    // How fastened a fitted moving part is.
    public const int Loose = 0;
    public const int Bolted = 1;
    public const int Fastened = 2;

    private readonly List<(EntityUid Vehicle, EntityUid Projectile)> _passedThrough = new();

    private bool _maskInsert;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUOpenVehicleComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMUOpenVehicleComponent, DamageModifyEvent>(OnDamageModify, before: [typeof(HardpointSystem)]);
        SubscribeLocalEvent<CMUOpenVehicleComponent, VehicleCanRunEvent>(OnCanRun);
        SubscribeLocalEvent<CMUOpenVehicleComponent, CMUVehicleCrashedEvent>(OnCrashed);
        SubscribeLocalEvent<CMUOpenVehicleComponent, PreventCollideEvent>(OnPreventCollide, after: [typeof(RequireProjectileTargetSystem)]);
        SubscribeLocalEvent<CMUOpenVehicleComponent, EntInsertedIntoContainerMessage>(OnSlotsChanged);
        SubscribeLocalEvent<CMUOpenVehicleComponent, EntRemovedFromContainerMessage>(OnSlotsChanged);
        SubscribeLocalEvent<CMUOpenVehicleComponent, ItemSlotInsertAttemptEvent>(OnSlotInsertAttempt);
        SubscribeLocalEvent<CMUOpenVehicleComponent, ItemSlotEjectAttemptEvent>(OnSlotEjectAttempt);
        SubscribeLocalEvent<CMUOpenVehicleComponent, CMUVehicleEngineRepairDoAfterEvent>(OnEngineRepair);
        SubscribeLocalEvent<CMUOpenVehicleComponent, CMUVehiclePartRemoveDoAfterEvent>(OnPartRemove);
        SubscribeLocalEvent<CMUOpenVehicleComponent, CMUVehiclePartRepairDoAfterEvent>(OnPartRepair);
        SubscribeLocalEvent<CMUOpenVehicleComponent, CMUVehiclePanelFastenDoAfterEvent>(OnPanelFasten);
        SubscribeLocalEvent<CMUVehiclePartComponent, InteractHandEvent>(OnPartInteractHand);
        SubscribeLocalEvent<CMUVehiclePartComponent, InteractUsingEvent>(OnPartInteractUsing);

        InitializeEngine();
        InitializeHeadlights();
        InitializeLamps();
    }

    private void OnMapInit(Entity<CMUOpenVehicleComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient || ent.Comp.PartEntities.Count > 0)
            return;

        foreach (var data in ent.Comp.Parts.Concat(LampParts(ent)))
        {
            var part = SpawnAttachedTo(ent.Comp.PartPrototype, new EntityCoordinates(ent, data.Offset));
            _metaData.SetEntityName(part, Loc.GetString(data.Name));

            var comp = EnsureComp<CMUVehiclePartComponent>(part);
            comp.Vehicle = ent;
            comp.Part = data.Id;
            comp.Slot = data.Slot;
            Dirty(part, comp);

            ent.Comp.PartEntities.Add(part);
        }

        RefreshParts(ent);
    }

    /// <summary>
    /// A dead engine won't run, and neither will a jeep whose hull is wrecked.
    /// </summary>
    private void OnCanRun(Entity<CMUOpenVehicleComponent> ent, ref VehicleCanRunEvent args)
    {
        if (ent.Comp.EngineIntegrity <= 0f || IsHullWrecked(ent))
            args.CanRun = false;
    }

    public bool IsHullWrecked(EntityUid vehicle)
    {
        return TryComp(vehicle, out HardpointIntegrityComponent? hull) &&
               hull.MaxIntegrity > 0f &&
               hull.Integrity <= 0f;
    }

    /// <summary>
    /// A head on crash shakes the engine too, by a share of what it does to the hull.
    /// </summary>
    private void OnCrashed(Entity<CMUOpenVehicleComponent> ent, ref CMUVehicleCrashedEvent args)
    {
        if (_net.IsClient || !args.Frontal || ent.Comp.EngineIntegrity <= 0f)
            return;

        ent.Comp.EngineIntegrity = MathF.Max(0f, ent.Comp.EngineIntegrity - args.HullDamage * ent.Comp.EngineCrashShare);
        Dirty(ent);
    }

    /// <summary>
    /// A stray shot passes through the open jeep to whoever is behind, leaving some of its damage in
    /// the bodywork. Shots fired from aboard don't count.
    /// </summary>
    private void OnPreventCollide(Entity<CMUOpenVehicleComponent> ent, ref PreventCollideEvent args)
    {
        if (_net.IsClient ||
            !args.Cancelled ||
            !TryComp(args.OtherEntity, out ProjectileComponent? projectile) ||
            IsAboard(projectile.Shooter, ent) ||
            IsAboard(projectile.Weapon, ent))
        {
            return;
        }

        if (EnsureComp<CMUPassedThroughComponent>(args.OtherEntity).Vehicles.Add(ent))
            _passedThrough.Add((ent, args.OtherEntity));
    }

    private bool IsAboard(EntityUid? entity, EntityUid vehicle)
    {
        for (var i = 0; i < 5 && entity is { } current && current.IsValid(); i++)
        {
            if (current == vehicle)
                return true;

            // The weapon or shooter may already be gone by the time its shot lands.
            if (!TryComp(current, out TransformComponent? xform))
                break;

            entity = xform.ParentUid;
        }

        return false;
    }

    private void OnSlotsChanged<T>(Entity<CMUOpenVehicleComponent> ent, ref T args) where T : ContainerModifiedMessage
    {
        if (_net.IsClient)
            return;

        if (args.Container.ID == JerryCanSlot && args is EntRemovedFromContainerMessage && ent.Comp.JerryCanLeaking)
            EnsureComp<CMUFuelLeakComponent>(args.Entity);

        // A refitted windshield goes on raised; one left folded with the hood open could neither
        // rise nor let the hood close. Refitted panels go on shut.
        if (args is EntRemovedFromContainerMessage)
        {
            foreach (var part in ent.Comp.Parts)
            {
                if (part.Slot != args.Container.ID)
                    continue;

                if (part.Kind == CMUVehiclePartKind.Windshield)
                    ent.Comp.WindshieldDown = false;
                else if (part.Kind == CMUVehiclePartKind.Panel)
                    ent.Comp.OpenPanels.Remove(part.Id);
            }

            ent.Comp.Fastening.Remove(args.Container.ID);
        }

        RefreshParts(ent);
    }

    /// <summary>
    /// Reads each hit before RMC routes it into the hardpoints and frame: the engine soaks up its
    /// share and the jeep's own parts wear by theirs.
    /// </summary>
    private void OnDamageModify(Entity<CMUOpenVehicleComponent> ent, ref DamageModifyEvent args)
    {
        if (_net.IsClient)
            return;

        var total = (float) DamageSpecifier.GetPositive(args.Damage).GetTotal();
        if (total <= 0f)
            return;

        var jeep = ent.Comp;
        AbsorbIntoEngine(ent, ref args, total);

        foreach (var part in jeep.Parts)
        {
            if (part.Kind == CMUVehiclePartKind.Windshield && part.Slot is { } windshield)
                WearPart(ent, windshield, total * jeep.WindshieldDamageShare);
        }
        WearLamps(ent, total);

        if (total >= jeep.PunctureMinDamage &&
            _random.Prob(jeep.JerryCanPunctureChance) &&
            TryGetPartSlot(ent.Owner, JerryCanSlot, out var slot) &&
            slot.Item is { } can)
        {
            EnsureComp<CMUFuelLeakComponent>(can);
        }

        RefreshParts(ent);
    }

    /// <summary>
    /// Whether a windshield is fitted, raised or folded.
    /// </summary>
    public bool HasWindshield(EntityUid vehicle, CMUOpenVehicleComponent jeep)
    {
        foreach (var part in jeep.Parts)
        {
            if (part.Kind == CMUVehiclePartKind.Windshield &&
                part.Slot is { } slotId &&
                TryGetPartSlot(vehicle, slotId, out var slot) &&
                slot.HasItem)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Wears the windshield by a shot it caught.
    /// </summary>
    public void WearWindshield(Entity<CMUOpenVehicleComponent> ent, float damage)
    {
        foreach (var part in ent.Comp.Parts)
        {
            if (part.Kind == CMUVehiclePartKind.Windshield && part.Slot is { } windshield)
                WearPart(ent, windshield, damage);
        }

        RefreshParts(ent);
    }

    private void WearPart(EntityUid vehicle, string slotId, float damage)
    {
        if (damage <= 0f ||
            !TryGetPartSlot(vehicle, slotId, out var slot) ||
            !TryComp(slot.Item, out CMUVehiclePartIntegrityComponent? part))
        {
            return;
        }

        var broken = part.MaxIntegrity * part.BrokenFraction;
        var wasWhole = part.Integrity > broken;
        part.Integrity = MathF.Max(0f, part.Integrity - damage);
        Dirty(slot.Item.Value, part);
        if (wasWhole && part.Integrity <= broken && _net.IsServer)
            _audio.PlayPvs(part.BreakSound, vehicle);
    }

    /// <summary>
    /// Recomputes the damage flags the client draws from, and lets each headlight beam shine only
    /// through an intact headlight.
    /// </summary>
    private void RefreshParts(Entity<CMUOpenVehicleComponent> ent)
    {
        var jeep = ent.Comp;
        Entity<ItemSlotsComponent?> vehicle = (ent.Owner, CompOrNull<ItemSlotsComponent>(ent.Owner));
        jeep.WindshieldDamaged = jeep.Parts.Exists(p => p.Kind == CMUVehiclePartKind.Windshield &&
                                                         p.Slot is { } windshield && IsBroken(vehicle, windshield));
        RefreshLamps(vehicle);
        jeep.JerryCanLeaking = TryGetPartSlot(vehicle, JerryCanSlot, out var can) &&
                               HasComp<CMUFuelLeakComponent>(can.Item);
        Dirty(ent);
        UpdateBeams(ent.Owner);
    }

    /// <summary>
    /// <see cref="ItemSlotsSystem.TryGetSlot"/> without its missing-component error: a jeep without
    /// item slots just has none of these parts.
    /// </summary>
    private bool TryGetPartSlot(Entity<ItemSlotsComponent?> vehicle, string slotId, [NotNullWhen(true)] out ItemSlot? slot)
    {
        slot = null;
        return Resolve(vehicle, ref vehicle.Comp, false) &&
               _itemSlots.TryGetSlot(vehicle, slotId, out slot);
    }

    private bool IsBroken(Entity<ItemSlotsComponent?> vehicle, string slotId)
    {
        return TryGetPartSlot(vehicle, slotId, out var slot) &&
               TryComp(slot.Item, out CMUVehiclePartIntegrityComponent? part) &&
               part.Integrity <= part.MaxIntegrity * part.BrokenFraction;
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        foreach (var (vehicle, projectileUid) in _passedThrough)
        {
            if (TryComp(vehicle, out CMUOpenVehicleComponent? jeep) &&
                TryComp(projectileUid, out ProjectileComponent? projectile))
            {
                _damageable.TryChangeDamage(vehicle, projectile.Damage * jeep.PassThroughDamage,
                    origin: projectile.Shooter, tool: projectileUid);
            }
        }

        _passedThrough.Clear();

        var leaks = EntityQueryEnumerator<CMUFuelLeakComponent>();
        while (leaks.MoveNext(out var uid, out var leak))
        {
            if (!_solution.TryGetSolution(uid, leak.Solution, out var solution, out var contents) || contents.Volume <= 0)
                continue;

            // Volumes hold whole hundredths, so a tick's share would be truncated away; drain only
            // what has built up to a whole hundredth and carry the rest.
            leak.Pending += leak.Rate * frameTime;
            var hundredths = (int) (leak.Pending * 100f);
            if (hundredths <= 0)
                continue;

            leak.Pending -= hundredths / 100f;
            _solution.SplitSolution(solution.Value, FixedPoint2.FromHundredths(hundredths));
        }
    }

    private void OnPartInteractHand(Entity<CMUVehiclePartComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled ||
            ent.Comp.Vehicle is not { } vehicle ||
            !TryComp(vehicle, out CMUOpenVehicleComponent? jeep))
        {
            return;
        }

        var user = args.User;

        // A lamp only comes off with a screwdriver.
        if (IsLamp(vehicle, ent.Comp.Part))
            return;

        var data = PartData(jeep, ent.Comp.Part);

        // A moving part with its screws and bolts out lifts off by hand.
        if (IsMovingPart(data) &&
            ent.Comp.Slot is { } looseSlot &&
            TryGetPartSlot(vehicle, looseSlot, out var loose) &&
            loose.HasItem &&
            GetFastening(jeep, looseSlot) == Loose)
        {
            if (IsPanelLoaded(vehicle, jeep, ent.Comp.Part))
                _popup.PopupClient(Loc.GetString("cmu-open-vehicle-part-loaded", ("part", Name(ent))), vehicle, user);
            else
                _itemSlots.TryEjectToHands(vehicle, loose, user, true);

            args.Handled = true;
            return;
        }

        switch (data?.Kind)
        {
            // Toggled by the server alone: a predicted flip is undone and redone while the server
            // catches up, which replays the swing animation.
            case CMUVehiclePartKind.Panel when _net.IsServer:
                if (!IsPanelFitted(vehicle, jeep, data.Id))
                    return;

                if (!GetPanelFlag(jeep, data.Id) && IsHeldShutByWindshield(vehicle, jeep, data.Id))
                    _popup.PopupEntity(Loc.GetString("cmu-open-vehicle-hood-blocked"), vehicle, user);
                else
                    SetPanel((vehicle, jeep), data.Id, !GetPanelFlag(jeep, data.Id));
                break;
            case CMUVehiclePartKind.Windshield when _net.IsServer:
                if (!IsPanelFitted(vehicle, jeep, data.Id))
                    return;

                if (!jeep.WindshieldDown && data.Interlock.Exists(p => GetPanelFlag(jeep, p) && IsPanelFitted(vehicle, jeep, p)))
                    _popup.PopupEntity(Loc.GetString("cmu-open-vehicle-windshield-blocked"), vehicle, user);
                else
                    SetWindshieldDown((vehicle, jeep), !jeep.WindshieldDown);
                break;
            case CMUVehiclePartKind.Engine when _net.IsServer:
                if (!IsExposed(vehicle, jeep, data.Id))
                    return;

                var percent = (int) MathF.Round(jeep.EngineIntegrity / jeep.EngineMaxIntegrity * 100);
                _popup.PopupEntity(Loc.GetString("cmu-open-vehicle-engine-examine", ("percent", percent)), vehicle, user);
                break;
            case CMUVehiclePartKind.Panel or CMUVehiclePartKind.Windshield or CMUVehiclePartKind.Engine:
                break;
            case null when ent.Comp.Part.StartsWith("seat_"):
                _seats.ClickSeat(ent.Owner, user);
                break;
            default:
                if (ent.Comp.Slot is not { } slotId ||
                    !TryGetPartSlot(vehicle, slotId, out var slot) ||
                    !slot.HasItem)
                {
                    return;
                }

                _itemSlots.TryEjectToHands(vehicle, slot, user, true);
                break;
        }

        args.Handled = true;
    }

    private void OnPartInteractUsing(Entity<CMUVehiclePartComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled ||
            ent.Comp.Vehicle is not { } vehicle ||
            !TryComp(vehicle, out CMUOpenVehicleComponent? jeep))
        {
            return;
        }

        var user = args.User;
        var used = args.Used;
        var data = PartData(jeep, ent.Comp.Part);
        var kind = data?.Kind;
        switch (ent.Comp.Part)
        {
            case var _ when kind == CMUVehiclePartKind.Wheels && HasComp<CMUVehicleSprayPainterComponent>(used):
                _paint.TryRespray(vehicle, user, used, CMUVehiclePaintZone.Wheels);
                args.Handled = true;
                return;
            case var seat when seat.StartsWith("seat_") && HasComp<CMUVehicleSprayPainterComponent>(used):
                _paint.TryRespray(vehicle, user, used, CMUVehiclePaintZone.Seats);
                args.Handled = true;
                return;
            case var door when _fuel.IsFuelDoor(vehicle, door) && _fuel.IsFuelCan(vehicle, used):
                if (!IsPanelOpen(vehicle, door))
                    _popup.PopupClient(Loc.GetString("cmu-vehicle-fuel-door-closed"), vehicle, user);
                else
                    _fuel.TryStartRefuel(vehicle, user, used);

                args.Handled = true;
                return;
            case var _ when kind == CMUVehiclePartKind.Engine && IsExposed(vehicle, jeep, ent.Comp.Part) &&
                               _tool.HasQuality(used, jeep.EngineRepairQuality):
                if (jeep.EngineIntegrity >= jeep.EngineMaxIntegrity)
                {
                    _popup.PopupClient(Loc.GetString("cmu-open-vehicle-engine-fine"), vehicle, user);
                }
                else
                {
                    _tool.UseTool(used, user, vehicle, (float) jeep.EngineRepairDelay.TotalSeconds,
                        jeep.EngineRepairQuality, new CMUVehicleEngineRepairDoAfterEvent(), fuel: 5f);
                }

                args.Handled = true;
                return;
            case var glass when (kind == CMUVehiclePartKind.Windshield || IsLamp(vehicle, glass)) &&
                                ent.Comp.Slot is { } worn &&
                                TryGetPartSlot(vehicle, worn, out var wornSlot) &&
                                TryComp(wornSlot.Item, out CMUVehiclePartIntegrityComponent? integrity) &&
                                _tool.HasQuality(used, jeep.PartRepairQuality):
                if (integrity.Integrity >= integrity.MaxIntegrity)
                {
                    _popup.PopupClient(Loc.GetString("cmu-open-vehicle-part-fine", ("part", Name(ent))), vehicle, user);
                }
                else
                {
                    _tool.UseTool(used, user, vehicle, (float) jeep.PartRepairDelay.TotalSeconds,
                        jeep.PartRepairQuality, new CMUVehiclePartRepairDoAfterEvent(worn), fuel: 5f);
                }

                args.Handled = true;
                return;
            case var lamp when IsLamp(vehicle, lamp) &&
                               ent.Comp.Slot is { } removable &&
                               TryGetPartSlot(vehicle, removable, out var fitted) &&
                               fitted.HasItem &&
                               _tool.HasQuality(used, jeep.PartRemoveQuality):
                _tool.UseTool(used, user, vehicle, (float) jeep.PartRemoveDelay.TotalSeconds,
                    jeep.PartRemoveQuality, new CMUVehiclePartRemoveDoAfterEvent(removable));
                args.Handled = true;
                return;
            // A wrench on a fastened part falls through to the jeep, which wrenches down the crate on
            // the bed.
            case var moving when IsMovingPart(data) &&
                                 ent.Comp.Slot is { } movingSlot &&
                                 HasItem(vehicle, movingSlot) &&
                                 TryWorkPart((vehicle, jeep), moving, movingSlot, user, used):
                args.Handled = true;
                return;
        }

        if (ent.Comp.Slot is { } slotId &&
            TryGetPartSlot(vehicle, slotId, out var slot) &&
            !slot.HasItem &&
            TryInsertByMask(vehicle, slotId, used, user))
        {
            args.Handled = true;
            return;
        }

        var ev = new InteractUsingEvent(user, used, vehicle, args.ClickLocation);
        RaiseLocalEvent(vehicle, ev);
        args.Handled = ev.Handled;
    }

    // Repairs repeat through the tool's wrapper do-after, which raises this same event instance on every
    // pass: Repeat has to go on the wrapper, and Handled is still set from the last pass, so it isn't checked.
    private void OnEngineRepair(Entity<CMUOpenVehicleComponent> ent, ref CMUVehicleEngineRepairDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        args.Handled = true;
        var jeep = ent.Comp;
        jeep.EngineIntegrity = MathF.Min(jeep.EngineMaxIntegrity, jeep.EngineIntegrity + jeep.EngineRepairAmount);
        Dirty(ent);
        args.Repeat = jeep.EngineIntegrity < jeep.EngineMaxIntegrity &&
                      jeep.Parts.Exists(p => p.Kind == CMUVehiclePartKind.Engine && IsExposed(ent, jeep, p.Id));
        args.Args.Event.Repeat = args.Repeat;
    }

    private void OnPartRepair(Entity<CMUOpenVehicleComponent> ent, ref CMUVehiclePartRepairDoAfterEvent args)
    {
        if (args.Cancelled ||
            !TryGetPartSlot(ent.Owner, args.Slot, out var slot) ||
            !TryComp(slot.Item, out CMUVehiclePartIntegrityComponent? part))
        {
            return;
        }

        args.Handled = true;
        part.Integrity = MathF.Min(part.MaxIntegrity, part.Integrity + ent.Comp.PartRepairAmount);
        Dirty(slot.Item.Value, part);
        args.Repeat = part.Integrity < part.MaxIntegrity;
        args.Args.Event.Repeat = args.Repeat;
        RefreshParts(ent);
    }

    private void OnPartRemove(Entity<CMUOpenVehicleComponent> ent, ref CMUVehiclePartRemoveDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !TryGetPartSlot(ent.Owner, args.Slot, out var slot))
            return;

        args.Handled = true;
        _itemSlots.TryEjectToHands(ent, slot, args.User, true);
    }

    private bool HasItem(EntityUid vehicle, string slotId)
    {
        return TryGetPartSlot(vehicle, slotId, out var slot) && slot.HasItem;
    }

    public static CMUVehiclePartData? PartData(CMUOpenVehicleComponent jeep, string part)
    {
        return jeep.Parts.Find(p => p.Id == part);
    }

    /// <summary>
    /// The parts that come off in steps: the windshield and the hinged panels.
    /// </summary>
    public static bool IsMovingPart(CMUVehiclePartData? part)
    {
        return part?.Kind is CMUVehiclePartKind.Panel or CMUVehiclePartKind.Windshield;
    }

    private static bool IsMovingPartSlot(CMUOpenVehicleComponent jeep, string slotId)
    {
        return jeep.Parts.Exists(p => p.Slot == slotId && IsMovingPart(p));
    }

    public static int GetFastening(CMUOpenVehicleComponent jeep, string slotId)
    {
        return jeep.Fastening.GetValueOrDefault(slotId, Fastened);
    }

    /// <summary>
    /// The panel the kit in a slot hangs on.
    /// </summary>
    private static string? KitHost(CMUOpenVehicleComponent jeep, string slotId)
    {
        return jeep.Parts.Find(p => p.Slot == slotId && p.Host != null)?.Host;
    }

    private bool IsPanelLoaded(EntityUid vehicle, CMUOpenVehicleComponent jeep, string panel)
    {
        foreach (var part in jeep.Parts)
        {
            if (part.Host == panel && part.Slot is { } slotId && HasItem(vehicle, slotId))
                return true;
        }

        return false;
    }

    /// <summary>
    /// A screwdriver takes a fastened part's screws out, or puts them back once it is bolted on; a
    /// wrench takes the bolts out of an unscrewed part, or bolts a loose one on. Anything else, or a
    /// wrench on a fastened part, is left to the jeep.
    /// </summary>
    private bool TryWorkPart(Entity<CMUOpenVehicleComponent> jeep, string part, string slot, EntityUid user, EntityUid tool)
    {
        var from = GetFastening(jeep.Comp, slot);
        int to;
        ProtoId<ToolQualityPrototype> quality;
        if (_tool.HasQuality(tool, jeep.Comp.PartRemoveQuality))
        {
            if (from == Loose)
            {
                _popup.PopupClient(Loc.GetString("cmu-open-vehicle-part-bolt-first", ("part", PartName(jeep, slot))), jeep, user);
                return true;
            }

            quality = jeep.Comp.PartRemoveQuality;
            to = from == Fastened ? Bolted : Fastened;
        }
        else if (_tool.HasQuality(tool, jeep.Comp.PanelBoltQuality) && from != Fastened)
        {
            quality = jeep.Comp.PanelBoltQuality;
            to = from == Bolted ? Loose : Bolted;
        }
        else
        {
            return false;
        }

        // What hangs on a panel comes off before the panel does.
        if (to < from && IsPanelLoaded(jeep, jeep.Comp, part))
        {
            _popup.PopupClient(Loc.GetString("cmu-open-vehicle-part-loaded", ("part", PartName(jeep, slot))), jeep, user);
            return true;
        }

        _tool.UseTool(tool, user, jeep, (float) jeep.Comp.PartRemoveDelay.TotalSeconds, quality,
            new CMUVehiclePanelFastenDoAfterEvent(slot, from, to));
        return true;
    }

    private void OnPanelFasten(Entity<CMUOpenVehicleComponent> ent, ref CMUVehiclePanelFastenDoAfterEvent args)
    {
        if (args.Cancelled ||
            args.Handled ||
            !HasItem(ent, args.Slot) ||
            GetFastening(ent.Comp, args.Slot) != args.From)
        {
            return;
        }

        args.Handled = true;
        if (args.To == Fastened)
            ent.Comp.Fastening.Remove(args.Slot);
        else
            ent.Comp.Fastening[args.Slot] = args.To;

        Dirty(ent);
        var message = (args.From, args.To) switch
        {
            (Fastened, Bolted) => "cmu-open-vehicle-part-unscrewed",
            (Bolted, Loose) => "cmu-open-vehicle-part-unbolted",
            (Loose, Bolted) => "cmu-open-vehicle-part-bolted",
            _ => "cmu-open-vehicle-part-screwed",
        };
        _popup.PopupClient(Loc.GetString(message, ("part", PartName(ent, args.Slot))), ent, args.User);
    }

    private string PartName(EntityUid vehicle, string slotId)
    {
        return TryGetPartSlot(vehicle, slotId, out var slot) && slot.Item is { } item ? Name(item) : slotId;
    }

    public static bool IsPanel(CMUOpenVehicleComponent jeep, string part)
    {
        return PartData(jeep, part)?.Kind == CMUVehiclePartKind.Panel;
    }

    public static bool HasPart(CMUOpenVehicleComponent jeep, string part)
    {
        return jeep.Parts.Exists(p => p.Id == part);
    }

    /// <summary>
    /// Whether the jeep has a panel on its hinges. One its parts list gives no slot can't come off.
    /// </summary>
    public bool IsPanelFitted(EntityUid vehicle, CMUOpenVehicleComponent jeep, string part)
    {
        if (jeep.Parts.Find(p => p.Id == part) is not { } data)
            return false;

        return data.Slot is not { } slotId ||
               !TryGetPartSlot(vehicle, slotId, out var slot) ||
               slot.HasItem;
    }

    public static bool GetPanelFlag(CMUOpenVehicleComponent jeep, string part)
    {
        return jeep.OpenPanels.Contains(part);
    }

    /// <summary>
    /// Whether nothing closes an opening: its panel stands open or has been taken off, or the
    /// vehicle has no such panel (the transport's fixed rear wall, or a seat with no door).
    /// </summary>
    public bool IsPanelOpen(EntityUid vehicle, string? part)
    {
        if (part == null ||
            !TryComp(vehicle, out CMUOpenVehicleComponent? jeep) ||
            !HasPart(jeep, part) ||
            !IsPanelFitted(vehicle, jeep, part))
        {
            return true;
        }

        return GetPanelFlag(jeep, part);
    }

    /// <summary>
    /// Whether a part can be reached: no panel covers it, or one that does is open or off, like the
    /// engine under the hood.
    /// </summary>
    public bool IsExposed(EntityUid vehicle, CMUOpenVehicleComponent jeep, string part)
    {
        var covered = false;
        foreach (var panel in jeep.Parts)
        {
            if (!panel.Exposes.Contains(part))
                continue;

            if (GetPanelFlag(jeep, panel.Id) || !IsPanelFitted(vehicle, jeep, panel.Id))
                return true;

            covered = true;
        }

        return !covered;
    }

    /// <summary>
    /// Whether a folded windshield lies on the panel, so it can't open.
    /// </summary>
    private bool IsHeldShutByWindshield(EntityUid vehicle, CMUOpenVehicleComponent jeep, string panel)
    {
        return jeep.WindshieldDown &&
               jeep.Parts.Exists(p => p.Kind == CMUVehiclePartKind.Windshield &&
                                      p.Interlock.Contains(panel) &&
                                      IsPanelFitted(vehicle, jeep, p.Id));
    }

    public void SetPanel(Entity<CMUOpenVehicleComponent> ent, string part, bool open)
    {
        if (GetPanelFlag(ent.Comp, part) == open)
            return;

        if (open)
            ent.Comp.OpenPanels.Add(part);
        else
            ent.Comp.OpenPanels.Remove(part);

        Dirty(ent);
        var data = PartData(ent.Comp, part);
        _audio.PlayPvs(open ? data?.OpenSound : data?.CloseSound, ent);
    }

    /// <summary>
    /// Kit hung on a panel can't go on or come off while the panel is off, nor while it stands open
    /// if it carries its kit out of reach, like the spare on the lowered tailgate.
    /// </summary>
    public bool IsKitBlocked(EntityUid vehicle, CMUOpenVehicleComponent jeep, string slotId)
    {
        if (KitHost(jeep, slotId) is not { } host || PartData(jeep, host) is not { } panel)
            return false;

        return !IsPanelFitted(vehicle, jeep, host) || panel.BlocksKitWhileOpen && GetPanelFlag(jeep, host);
    }

    private bool TryInsertByMask(EntityUid vehicle, string slotId, EntityUid used, EntityUid user)
    {
        _maskInsert = true;
        try
        {
            return _itemSlots.TryInsert(vehicle, slotId, used, user);
        }
        finally
        {
            _maskInsert = false;
        }
    }

    private void OnSlotInsertAttempt(Entity<CMUOpenVehicleComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Cancelled || args.Slot.ID is not { } slotId)
            return;

        if (args.User != null && !_maskInsert && ent.Comp.Parts.Exists(p => p.Slot == slotId && p.MaskOnly))
        {
            args.Cancelled = true;
            return;
        }

        if (IsKitBlocked(ent, ent.Comp, slotId))
        {
            args.Cancelled = true;
            if (args.User is { } user)
                _popup.PopupClient(Loc.GetString("cmu-open-vehicle-kit-blocked"), ent, user);

            return;
        }

        // A moving part hung on by hand still has to be bolted and screwed down. The ones a jeep is
        // built with go in with no one inserting them, and come fastened.
        if (_net.IsServer && args.User != null && IsMovingPartSlot(ent.Comp, slotId))
        {
            ent.Comp.Fastening[slotId] = Loose;
            Dirty(ent);
        }
    }

    private void OnSlotEjectAttempt(Entity<CMUOpenVehicleComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        if (!args.Cancelled && args.Slot.ID is { } slotId && IsKitBlocked(ent, ent.Comp, slotId))
            args.Cancelled = true;
    }

    public void SetWindshieldDown(Entity<CMUOpenVehicleComponent> ent, bool down)
    {
        if (ent.Comp.WindshieldDown == down)
            return;

        ent.Comp.WindshieldDown = down;
        Dirty(ent);
        _audio.PlayPvs(ent.Comp.WindshieldSound, ent);
    }
}
