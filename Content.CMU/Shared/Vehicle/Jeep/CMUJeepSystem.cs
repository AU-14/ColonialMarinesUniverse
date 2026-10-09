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

namespace Content.Shared.CMU14.Vehicle.Jeep;

public sealed partial class CMUJeepSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private CMUVehicleFuelSystem _fuel = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedPointLightSystem _lights = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private INetManager _net = default!;
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

    // Hinged panels, by part id.
    public const string Hood = "hood";
    public const string DriverDoor = "door_driver";
    public const string PassengerDoor = "door_passenger";
    public const string Tailgate = "tailgate";

    // How fastened a fitted moving part is.
    public const int Loose = 0;
    public const int Bolted = 1;
    public const int Fastened = 2;

    private readonly List<(EntityUid Vehicle, EntityUid Projectile)> _passedThrough = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUJeepComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMUJeepComponent, DamageModifyEvent>(OnDamageModify, before: [typeof(HardpointSystem)]);
        SubscribeLocalEvent<CMUJeepComponent, VehicleCanRunEvent>(OnCanRun);
        SubscribeLocalEvent<CMUJeepComponent, PreventCollideEvent>(OnPreventCollide, after: [typeof(RequireProjectileTargetSystem)]);
        SubscribeLocalEvent<CMUJeepComponent, EntInsertedIntoContainerMessage>(OnSlotsChanged);
        SubscribeLocalEvent<CMUJeepComponent, EntRemovedFromContainerMessage>(OnSlotsChanged);
        SubscribeLocalEvent<CMUJeepComponent, ItemSlotInsertAttemptEvent>(OnSlotInsertAttempt);
        SubscribeLocalEvent<CMUJeepComponent, ItemSlotEjectAttemptEvent>(OnSlotEjectAttempt);
        SubscribeLocalEvent<CMUJeepComponent, CMUJeepEngineRepairDoAfterEvent>(OnEngineRepair);
        SubscribeLocalEvent<CMUJeepComponent, CMUJeepPartRemoveDoAfterEvent>(OnPartRemove);
        SubscribeLocalEvent<CMUJeepComponent, CMUJeepPartRepairDoAfterEvent>(OnPartRepair);
        SubscribeLocalEvent<CMUJeepComponent, CMUJeepPanelFastenDoAfterEvent>(OnPanelFasten);
        SubscribeLocalEvent<CMUVehiclePartComponent, InteractHandEvent>(OnPartInteractHand);
        SubscribeLocalEvent<CMUVehiclePartComponent, InteractUsingEvent>(OnPartInteractUsing);

        InitializeHeadlights();
        InitializeLamps();
    }

    private void OnMapInit(Entity<CMUJeepComponent> ent, ref MapInitEvent args)
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

    private void OnCanRun(Entity<CMUJeepComponent> ent, ref VehicleCanRunEvent args)
    {
        if (ent.Comp.EngineIntegrity <= 0f)
            args.CanRun = false;
    }

    /// <summary>
    /// A stray shot passes through the open jeep to whoever is behind, leaving some of its damage in
    /// the bodywork. Shots fired from aboard don't count.
    /// </summary>
    private void OnPreventCollide(Entity<CMUJeepComponent> ent, ref PreventCollideEvent args)
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

    private void OnSlotsChanged<T>(Entity<CMUJeepComponent> ent, ref T args) where T : ContainerModifiedMessage
    {
        if (_net.IsClient)
            return;

        if (args.Container.ID == JerryCanSlot && args is EntRemovedFromContainerMessage && ent.Comp.JerryCanLeaking)
            EnsureComp<CMUFuelLeakComponent>(args.Entity);

        // A refitted windshield goes on raised; one left folded with the hood open could neither
        // rise nor let the hood close. Refitted panels go on shut.
        if (args is EntRemovedFromContainerMessage)
        {
            if (args.Container.ID == WindshieldSlot)
                ent.Comp.WindshieldDown = false;

            foreach (var part in ent.Comp.Parts)
            {
                if (part.Slot == args.Container.ID && IsPanel(part.Id))
                    SetPanelFlag(ent.Comp, part.Id, false);
            }

            ent.Comp.Fastening.Remove(args.Container.ID);
        }

        RefreshParts(ent);
    }

    /// <summary>
    /// Reads each hit before RMC routes it into the hardpoints and frame, and wears the jeep's own
    /// parts by their shares of it.
    /// </summary>
    private void OnDamageModify(Entity<CMUJeepComponent> ent, ref DamageModifyEvent args)
    {
        if (_net.IsClient)
            return;

        var total = (float) DamageSpecifier.GetPositive(args.Damage).GetTotal();
        if (total <= 0f)
            return;

        var jeep = ent.Comp;
        var engineWasRunning = jeep.EngineIntegrity > 0f;
        jeep.EngineIntegrity = MathF.Max(0f, jeep.EngineIntegrity - total * jeep.EngineDamageShare);
        if (engineWasRunning && jeep.EngineIntegrity <= 0f && TryComp(ent, out VehicleComponent? vehicle) && vehicle.Operator is { } driver)
            _popup.PopupEntity(Loc.GetString("cmu-jeep-engine-dead"), ent, driver, PopupType.MediumCaution);

        WearPart(ent, WindshieldSlot, total * jeep.WindshieldDamageShare);
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

    private void WearPart(EntityUid vehicle, string slotId, float damage)
    {
        if (damage <= 0f ||
            !TryGetPartSlot(vehicle, slotId, out var slot) ||
            !TryComp(slot.Item, out CMUJeepPartIntegrityComponent? part))
        {
            return;
        }

        part.Integrity = MathF.Max(0f, part.Integrity - damage);
        Dirty(slot.Item.Value, part);
    }

    /// <summary>
    /// Recomputes the damage flags the client draws from, and lets each headlight beam shine only
    /// through an intact headlight.
    /// </summary>
    private void RefreshParts(Entity<CMUJeepComponent> ent)
    {
        var jeep = ent.Comp;
        Entity<ItemSlotsComponent?> vehicle = (ent.Owner, CompOrNull<ItemSlotsComponent>(ent.Owner));
        jeep.WindshieldDamaged = IsBroken(vehicle, WindshieldSlot);
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
               TryComp(slot.Item, out CMUJeepPartIntegrityComponent? part) &&
               part.Integrity <= part.MaxIntegrity * part.BrokenFraction;
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        foreach (var (vehicle, projectileUid) in _passedThrough)
        {
            if (TryComp(vehicle, out CMUJeepComponent? jeep) &&
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
            !TryComp(vehicle, out CMUJeepComponent? jeep))
        {
            return;
        }

        var user = args.User;

        // A lamp only comes off with a screwdriver.
        if (IsLamp(vehicle, ent.Comp.Part))
            return;

        // A moving part with its screws and bolts out lifts off by hand.
        if (IsMovingPart(ent.Comp.Part) &&
            ent.Comp.Slot is { } looseSlot &&
            TryGetPartSlot(vehicle, looseSlot, out var loose) &&
            loose.HasItem &&
            GetFastening(jeep, looseSlot) == Loose)
        {
            if (IsPanelLoaded(vehicle, ent.Comp.Part))
                _popup.PopupClient(Loc.GetString("cmu-jeep-part-loaded", ("part", Name(ent))), vehicle, user);
            else
                _itemSlots.TryEjectToHands(vehicle, loose, user, true);

            args.Handled = true;
            return;
        }

        switch (ent.Comp.Part)
        {
            // Toggled by the server alone: a predicted flip is undone and redone while the server
            // catches up, which replays the swing animation.
            case Hood when _net.IsServer:
                if (!IsPanelFitted(vehicle, jeep, Hood))
                    return;

                if (jeep.WindshieldDown && HasWindshield(vehicle))
                    _popup.PopupEntity(Loc.GetString("cmu-jeep-hood-blocked"), vehicle, user);
                else
                    SetHood((vehicle, jeep), !jeep.HoodOpen);
                break;
            case "windshield" when _net.IsServer:
                if (!HasWindshield(vehicle))
                    return;

                if (jeep.HoodOpen && IsPanelFitted(vehicle, jeep, Hood))
                    _popup.PopupEntity(Loc.GetString("cmu-jeep-windshield-blocked"), vehicle, user);
                else
                    SetWindshieldDown((vehicle, jeep), !jeep.WindshieldDown);
                break;
            case "fuel_door" when _net.IsServer:
                jeep.FuelDoorOpen = !jeep.FuelDoorOpen;
                Dirty(vehicle, jeep);
                break;
            case DriverDoor or PassengerDoor or Tailgate when _net.IsServer:
                if (!IsPanelFitted(vehicle, jeep, ent.Comp.Part))
                    return;

                SetPanel((vehicle, jeep), ent.Comp.Part, !GetPanelFlag(jeep, ent.Comp.Part));
                break;
            case "engine" when _net.IsServer:
                if (!IsHoodOpen(vehicle, jeep))
                    return;

                var percent = (int) MathF.Round(jeep.EngineIntegrity / jeep.EngineMaxIntegrity * 100);
                _popup.PopupEntity(Loc.GetString("cmu-jeep-engine-examine", ("percent", percent)), vehicle, user);
                break;
            case Hood or "windshield" or "fuel_door" or "engine" or DriverDoor or PassengerDoor or Tailgate:
                break;
            case var seat when seat.StartsWith("seat_"):
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
            !TryComp(vehicle, out CMUJeepComponent? jeep))
        {
            return;
        }

        var user = args.User;
        var used = args.Used;
        switch (ent.Comp.Part)
        {
            case "fuel_door" when _fuel.IsFuelCan(vehicle, used):
                if (!jeep.FuelDoorOpen)
                {
                    _popup.PopupClient(Loc.GetString("cmu-vehicle-fuel-door-closed"), vehicle, user);
                    args.Handled = true;
                    return;
                }

                if (_fuel.TryStartRefuel(vehicle, user, used))
                {
                    args.Handled = true;
                    return;
                }

                break;
            case "engine" when IsHoodOpen(vehicle, jeep) && _tool.HasQuality(used, jeep.EngineRepairQuality):
                if (jeep.EngineIntegrity >= jeep.EngineMaxIntegrity)
                {
                    _popup.PopupClient(Loc.GetString("cmu-jeep-engine-fine"), vehicle, user);
                }
                else
                {
                    _tool.UseTool(used, user, vehicle, (float) jeep.EngineRepairDelay.TotalSeconds,
                        jeep.EngineRepairQuality, new CMUJeepEngineRepairDoAfterEvent(), fuel: 5f);
                }

                args.Handled = true;
                return;
            case var glass when (glass == "windshield" || IsLamp(vehicle, glass)) &&
                                ent.Comp.Slot is { } worn &&
                                TryGetPartSlot(vehicle, worn, out var wornSlot) &&
                                TryComp(wornSlot.Item, out CMUJeepPartIntegrityComponent? integrity) &&
                                _tool.HasQuality(used, jeep.PartRepairQuality):
                if (integrity.Integrity >= integrity.MaxIntegrity)
                {
                    _popup.PopupClient(Loc.GetString("cmu-jeep-part-fine", ("part", Name(ent))), vehicle, user);
                }
                else
                {
                    _tool.UseTool(used, user, vehicle, (float) jeep.PartRepairDelay.TotalSeconds,
                        jeep.PartRepairQuality, new CMUJeepPartRepairDoAfterEvent(worn), fuel: 5f);
                }

                args.Handled = true;
                return;
            case var lamp when IsLamp(vehicle, lamp) &&
                               ent.Comp.Slot is { } removable &&
                               TryGetPartSlot(vehicle, removable, out var fitted) &&
                               fitted.HasItem &&
                               _tool.HasQuality(used, jeep.PartRemoveQuality):
                _tool.UseTool(used, user, vehicle, (float) jeep.PartRemoveDelay.TotalSeconds,
                    jeep.PartRemoveQuality, new CMUJeepPartRemoveDoAfterEvent(removable));
                args.Handled = true;
                return;
            // A wrench on a fastened part falls through to the jeep, which wrenches down the crate on
            // the bed.
            case var moving when IsMovingPart(moving) &&
                                 ent.Comp.Slot is { } movingSlot &&
                                 HasItem(vehicle, movingSlot) &&
                                 TryWorkPart((vehicle, jeep), moving, movingSlot, user, used):
                args.Handled = true;
                return;
        }

        if (ent.Comp.Slot is { } slotId &&
            TryGetPartSlot(vehicle, slotId, out var slot) &&
            !slot.HasItem &&
            _itemSlots.TryInsert(vehicle, slotId, used, user))
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
    private void OnEngineRepair(Entity<CMUJeepComponent> ent, ref CMUJeepEngineRepairDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        args.Handled = true;
        var jeep = ent.Comp;
        jeep.EngineIntegrity = MathF.Min(jeep.EngineMaxIntegrity, jeep.EngineIntegrity + jeep.EngineRepairAmount);
        Dirty(ent);
        args.Repeat = jeep.HoodOpen && jeep.EngineIntegrity < jeep.EngineMaxIntegrity;
        args.Args.Event.Repeat = args.Repeat;
    }

    private void OnPartRepair(Entity<CMUJeepComponent> ent, ref CMUJeepPartRepairDoAfterEvent args)
    {
        if (args.Cancelled ||
            !TryGetPartSlot(ent.Owner, args.Slot, out var slot) ||
            !TryComp(slot.Item, out CMUJeepPartIntegrityComponent? part))
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

    private void OnPartRemove(Entity<CMUJeepComponent> ent, ref CMUJeepPartRemoveDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !TryGetPartSlot(ent.Owner, args.Slot, out var slot))
            return;

        args.Handled = true;
        _itemSlots.TryEjectToHands(ent, slot, args.User, true);
    }

    private bool HasWindshield(EntityUid vehicle)
    {
        return HasItem(vehicle, WindshieldSlot);
    }

    private bool HasItem(EntityUid vehicle, string slotId)
    {
        return TryGetPartSlot(vehicle, slotId, out var slot) && slot.HasItem;
    }

    /// <summary>
    /// The parts that come off in steps: the windshield and the hinged panels.
    /// </summary>
    public static bool IsMovingPart(string part)
    {
        return part == "windshield" || IsPanel(part);
    }

    private static bool IsMovingPartSlot(CMUJeepComponent jeep, string slotId)
    {
        return jeep.Parts.Exists(p => p.Slot == slotId && IsMovingPart(p.Id));
    }

    public static int GetFastening(CMUJeepComponent jeep, string slotId)
    {
        return jeep.Fastening.GetValueOrDefault(slotId, Fastened);
    }

    /// <summary>
    /// The panel kit hangs on: the shovel and the axe on the driver's door, the spare and the jerry
    /// can on the tailgate.
    /// </summary>
    private static string? KitHost(string slotId)
    {
        return slotId switch
        {
            ShovelSlot or AxeSlot => DriverDoor,
            SpareSlot or JerryCanSlot => Tailgate,
            _ => null,
        };
    }

    private bool IsPanelLoaded(EntityUid vehicle, string panel)
    {
        foreach (var slotId in new[] { ShovelSlot, AxeSlot, SpareSlot, JerryCanSlot })
        {
            if (KitHost(slotId) == panel && HasItem(vehicle, slotId))
                return true;
        }

        return false;
    }

    /// <summary>
    /// A screwdriver takes a fastened part's screws out, or puts them back once it is bolted on; a
    /// wrench takes the bolts out of an unscrewed part, or bolts a loose one on. Anything else, or a
    /// wrench on a fastened part, is left to the jeep.
    /// </summary>
    private bool TryWorkPart(Entity<CMUJeepComponent> jeep, string part, string slot, EntityUid user, EntityUid tool)
    {
        var from = GetFastening(jeep.Comp, slot);
        int to;
        ProtoId<ToolQualityPrototype> quality;
        if (_tool.HasQuality(tool, jeep.Comp.PartRemoveQuality))
        {
            if (from == Loose)
            {
                _popup.PopupClient(Loc.GetString("cmu-jeep-part-bolt-first", ("part", PartName(jeep, slot))), jeep, user);
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
        if (to < from && IsPanelLoaded(jeep, part))
        {
            _popup.PopupClient(Loc.GetString("cmu-jeep-part-loaded", ("part", PartName(jeep, slot))), jeep, user);
            return true;
        }

        _tool.UseTool(tool, user, jeep, (float) jeep.Comp.PartRemoveDelay.TotalSeconds, quality,
            new CMUJeepPanelFastenDoAfterEvent(slot, from, to));
        return true;
    }

    private void OnPanelFasten(Entity<CMUJeepComponent> ent, ref CMUJeepPanelFastenDoAfterEvent args)
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
            (Fastened, Bolted) => "cmu-jeep-part-unscrewed",
            (Bolted, Loose) => "cmu-jeep-part-unbolted",
            (Loose, Bolted) => "cmu-jeep-part-bolted",
            _ => "cmu-jeep-part-screwed",
        };
        _popup.PopupClient(Loc.GetString(message, ("part", PartName(ent, args.Slot))), ent, args.User);
    }

    private string PartName(EntityUid vehicle, string slotId)
    {
        return TryGetPartSlot(vehicle, slotId, out var slot) && slot.Item is { } item ? Name(item) : slotId;
    }

    public static bool IsPanel(string part)
    {
        return part is Hood or DriverDoor or PassengerDoor or Tailgate;
    }

    public static bool HasPart(CMUJeepComponent jeep, string part)
    {
        return jeep.Parts.Exists(p => p.Id == part);
    }

    /// <summary>
    /// Whether the jeep has a panel on its hinges. One its parts list gives no slot can't come off.
    /// </summary>
    public bool IsPanelFitted(EntityUid vehicle, CMUJeepComponent jeep, string part)
    {
        if (jeep.Parts.Find(p => p.Id == part) is not { } data)
            return false;

        return data.Slot is not { } slotId ||
               !TryGetPartSlot(vehicle, slotId, out var slot) ||
               slot.HasItem;
    }

    public static bool GetPanelFlag(CMUJeepComponent jeep, string part)
    {
        return part switch
        {
            Hood => jeep.HoodOpen,
            DriverDoor => jeep.DriverDoorOpen,
            PassengerDoor => jeep.PassengerDoorOpen,
            Tailgate => jeep.TailgateOpen,
            _ => false,
        };
    }

    private static void SetPanelFlag(CMUJeepComponent jeep, string part, bool open)
    {
        switch (part)
        {
            case Hood:
                jeep.HoodOpen = open;
                break;
            case DriverDoor:
                jeep.DriverDoorOpen = open;
                break;
            case PassengerDoor:
                jeep.PassengerDoorOpen = open;
                break;
            case Tailgate:
                jeep.TailgateOpen = open;
                break;
        }
    }

    /// <summary>
    /// Whether nothing closes an opening: its panel stands open or has been taken off, or the
    /// vehicle has no such panel (the transport's fixed rear wall, or a seat with no door).
    /// </summary>
    public bool IsPanelOpen(EntityUid vehicle, string? part)
    {
        if (part == null ||
            !TryComp(vehicle, out CMUJeepComponent? jeep) ||
            !HasPart(jeep, part) ||
            !IsPanelFitted(vehicle, jeep, part))
        {
            return true;
        }

        return GetPanelFlag(jeep, part);
    }

    /// <summary>
    /// The engine can be reached with the hood up or taken off.
    /// </summary>
    public bool IsHoodOpen(EntityUid vehicle, CMUJeepComponent jeep)
    {
        return jeep.HoodOpen || !IsPanelFitted(vehicle, jeep, Hood);
    }

    public void SetPanel(Entity<CMUJeepComponent> ent, string part, bool open)
    {
        if (part == Hood)
        {
            SetHood(ent, open);
            return;
        }

        if (GetPanelFlag(ent.Comp, part) == open)
            return;

        SetPanelFlag(ent.Comp, part, open);
        Dirty(ent);
        var sound = part == Tailgate
            ? open ? ent.Comp.TailgateOpenSound : ent.Comp.TailgateCloseSound
            : open ? ent.Comp.DoorOpenSound : ent.Comp.DoorCloseSound;
        _audio.PlayPvs(sound, ent);
    }

    /// <summary>
    /// Kit hung on a panel can't go on or come off while the panel is off; the spare and the jerry
    /// can are also out of reach while the tailgate is down.
    /// </summary>
    public bool IsKitBlocked(EntityUid vehicle, CMUJeepComponent jeep, string slotId)
    {
        if (KitHost(slotId) is not { } host || !HasPart(jeep, host))
            return false;

        return !IsPanelFitted(vehicle, jeep, host) || host == Tailgate && jeep.TailgateOpen;
    }

    private void OnSlotInsertAttempt(Entity<CMUJeepComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Cancelled || args.Slot.ID is not { } slotId)
            return;

        if (IsKitBlocked(ent, ent.Comp, slotId))
        {
            args.Cancelled = true;
            if (args.User is { } user)
                _popup.PopupClient(Loc.GetString("cmu-jeep-kit-blocked"), ent, user);

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

    private void OnSlotEjectAttempt(Entity<CMUJeepComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        if (!args.Cancelled && args.Slot.ID is { } slotId && IsKitBlocked(ent, ent.Comp, slotId))
            args.Cancelled = true;
    }

    public void SetHood(Entity<CMUJeepComponent> ent, bool open)
    {
        if (ent.Comp.HoodOpen == open)
            return;

        ent.Comp.HoodOpen = open;
        Dirty(ent);
        _audio.PlayPvs(open ? ent.Comp.HoodOpenSound : ent.Comp.HoodCloseSound, ent);
    }

    public void SetWindshieldDown(Entity<CMUJeepComponent> ent, bool down)
    {
        if (ent.Comp.WindshieldDown == down)
            return;

        ent.Comp.WindshieldDown = down;
        Dirty(ent);
        _audio.PlayPvs(ent.Comp.WindshieldSound, ent);
    }
}
