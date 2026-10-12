using System.Linq;
using Content.Client.Clickable;
using Content.Client.CombatMode;
using Content.Client.Gameplay;
using Content.Client.Interactable.Components;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.Vehicle;
using Content.Shared.CMU14.Vehicle.Jeep;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// Draws an open vehicle's hinged panels, windshield and stowed kit from its state with their swing
/// animations (kit hung on a panel swings with it), keeps its clickable part masks on the vehicle's
/// frame and outlines the part under the mouse. Which parts there are and how they behave comes from
/// the vehicle's part list; how long a swing takes, from its art.
/// </summary>
public sealed class CMUJeepVisualSystem : EntitySystem
{
    [Dependency] private CombatModeSystem _combat = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private CMUVehicleFrontDamageSystem _frontDamage = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private CMUJeepSystem _jeepSystem = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IStateManager _state = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private VehicleTurretSystem _turret = default!;
    [Dependency] private CMUVehicleTyreSystem _tyres = default!;


    // Part outlines are tinted like the standard hover outline: green in reach, red out of reach.
    private static readonly Color InReachColor = new(0f, 1f, 0f, 0.6f);
    private static readonly Color OutOfReachColor = new(1f, 0f, 0f, 0.6f);

    private readonly Dictionary<EntityUid, Dictionary<string, Swing>> _swings = new();

    private sealed class Swing
    {
        public bool? Shown;
        public TimeSpan? Start;
    }

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(CMUVehicleOverlayVisualSystem));
        SubscribeLocalEvent<CMUJeepComponent, ComponentShutdown>((uid, _, _) => _swings.Remove(uid));
    }

    public override void FrameUpdate(float frameTime)
    {
        var hovered = GetHovered();

        var jeeps = EntityQueryEnumerator<CMUJeepComponent, SpriteComponent>();
        while (jeeps.MoveNext(out var uid, out var jeep, out var sprite))
        {
            // A jeep out of view is detached, not deleted. Forget its swings, so a change made
            // while nobody nearby watched shows as done when it comes back.
            if ((MetaData(uid).Flags & MetaDataFlags.Detached) != 0)
            {
                _swings.Remove(uid);
                continue;
            }

            Entity<SpriteComponent>? overlay = null;
            if (TryComp(uid, out CMUVehicleOverlayComponent? over) &&
                TryComp(over.Overlay, out SpriteComponent? overSprite))
            {
                overlay = (over.Overlay.Value, overSprite);
            }

            if (!_swings.TryGetValue(uid, out var swings))
                _swings[uid] = swings = new Dictionary<string, Swing>();

            // A wrecked hull shows at a glance: the hood popped ajar, the windshield cracked, the body
            // sagging on its wheels (CMUVehicleOverlayVisualSystem) and the frame damage at its worst.
            var hullWrecked = _jeepSystem.IsHullWrecked(uid);
            var raised = new HashSet<string>();
            foreach (var part in jeep.Parts)
            {
                var id = part.Id;
                switch (part.Kind)
                {
                    case CMUVehiclePartKind.Panel:
                    {
                        var swing = SwingOf(swings, id);
                        var open = CMUJeepSystem.GetPanelFlag(jeep, id);
                        Animate((uid, sprite), overlay, id, swing, open, StateLength(sprite, $"{id}_opening"),
                            $"{id}_open", ClosedState(uid, id), $"{id}_opening", $"{id}_closing", part.RaisedLayer);
                        var up = open || swing.Start != null;
                        if (part.AjarWhenWrecked && hullWrecked && !up)
                        {
                            var ajar = AjarTime(sprite, $"{id}_opening");
                            SetState((uid, sprite), id, $"{id}_opening", ajar);
                            if (overlay != null && part.RaisedLayer is { } raisedLayer)
                                SetState(overlay.Value, raisedLayer, $"{id}_opening_overlay", ajar);

                            up = true;
                        }

                        if (up)
                            raised.Add(id);
                        break;
                    }
                    case CMUVehiclePartKind.Windshield:
                    {
                        var glass = jeep.WindshieldDamaged || hullWrecked ? "1" : "0";
                        Animate((uid, sprite), overlay, id, SwingOf(swings, id), jeep.WindshieldDown,
                            StateLength(sprite, $"{id}_folding_{glass}"), $"{id}_down_{glass}", $"{id}_up_{glass}",
                            $"{id}_folding_{glass}", $"{id}_raising_{glass}");
                        break;
                    }
                    // Kit swings with the panel it hangs on, a leaking jerry can drawn holed.
                    case CMUVehiclePartKind.Item when part.Slot != null:
                    {
                        var look = part.Slot == CMUJeepSystem.JerryCanSlot && jeep.JerryCanLeaking ? "1" : "0";
                        var prefix = look == "0" && _itemSlots.TryGetSlot(uid, part.Slot, out var kitSlot)
                            ? _tyres.StatePrefix(kitSlot.Item)
                            : string.Empty;
                        AnimateKit((uid, sprite), overlay, swings, jeep, id, part.Host, look, prefix);
                        break;
                    }
                }
            }

            // Each lamp shows while fitted, cracked once broken.
            if (TryComp(uid, out CMUJeepLampsComponent? lamps))
            {
                foreach (var lamp in lamps.Lamps)
                {
                    var look = lamps.Broken.Contains(lamp.Id) ? "1" : "0";
                    SetState((uid, sprite), lamp.Id, $"{lamp.Id}_{look}");
                    SetVisible((uid, sprite), lamp.Id, lamp.Slot.HasItem);
                    if (overlay == null)
                        continue;

                    SetState(overlay.Value, lamp.Id, $"{lamp.Id}_overlay_{look}");
                    SetVisible(overlay.Value, lamp.Id, lamp.Slot.HasItem);
                }
            }

            var engine = jeep.EngineIntegrity / jeep.EngineMaxIntegrity;
            SetState((uid, sprite), "engine", engine < jeep.EngineSmokeFraction ? "engine_1" : "engine_0");
            if (overlay != null)
            {
                SetState(overlay.Value, "smoke", engine < jeep.EngineHeavySmokeFraction ? "engine_smoke_1" : "engine_smoke_0");
                SetVisible(overlay.Value, "smoke", engine < jeep.EngineSmokeFraction);
                SetVisible(overlay.Value, "fire", jeep.Burning);
            }
            TryComp(uid, out ItemSlotsComponent? slots);
            foreach (var part in jeep.Parts)
            {
                if (part.Slot == null || part.Kind == CMUVehiclePartKind.Wheels)
                    continue;

                var fitted = IsFitted(uid, slots, part.Slot);
                SetVisible((uid, sprite), part.Id, fitted);
                if (overlay == null)
                    continue;

                // Shut, the hood goes under the windshield, which folds down onto it; raised, the lid
                // stands in front of it.
                if (part.RaisedLayer is { } raisedLayer)
                {
                    var up = raised.Contains(part.Id);
                    SetVisible(overlay.Value, raisedLayer, fitted && up);
                    fitted &= !up;
                }

                SetVisible(overlay.Value, part.Id, fitted);
            }

            if (TryComp(uid, out CMUVehicleCargoComponent? cargo) && overlay != null)
                SetVisible(overlay.Value, "straps", cargo.Crate != null && cargo.Secured);

            if (overlay != null)
            {
                var outline = hovered is { } h &&
                              TryComp(h, out CMUVehiclePartComponent? hoveredPart) &&
                              hoveredPart.Vehicle == uid
                    ? OutlineState(hoveredPart, jeep, SeatOpened(uid, jeep, hoveredPart.Part))
                    : null;

                if (outline != null)
                {
                    SetState(overlay.Value, "hover", outline);
                    if (_sprite.LayerMapTryGet(overlay.Value.AsNullable(), "hover", out var hover, false))
                        _sprite.LayerSetColor(overlay.Value.AsNullable(), hover, InReach(hovered!.Value) ? InReachColor : OutOfReachColor);
                }

                SetVisible(overlay.Value, "hover", outline != null);
            }
        }

        var combat = _combat.IsInCombatMode();
        UpdateTurrets(combat);
        var parts = EntityQueryEnumerator<CMUVehiclePartComponent, SpriteComponent, TransformComponent>();
        while (parts.MoveNext(out var uid, out var part, out var sprite, out var xform))
        {
            if (part.Vehicle is not { } vehicle ||
                !TryComp(vehicle, out CMUJeepComponent? jeep) ||
                !TryComp(vehicle, out SpriteComponent? vehicleSprite) ||
                !_sprite.LayerMapTryGet((uid, sprite), "mask", out var mask, false))
            {
                continue;
            }

            if (vehicleSprite.BaseRSI is { } rsi && sprite[mask].ActualRsi != rsi)
                _sprite.LayerSetRsi((uid, sprite), mask, rsi);

            SetState((uid, sprite), "mask", MaskState(part, jeep, SeatOpened(vehicle, jeep, part.Part)));

            // A hidden mask drops out of the click test, so in combat mode attacks and aimed shots land
            // on the jeep itself. An empty slot's mask shows, a faint ghost of what goes there, only
            // while what is in hand would go in, so it is fitted where it is clicked; otherwise the jeep's
            // own item slots take it.
            TryComp(vehicle, out ItemSlotsComponent? vehicleSlots);
            var data = CMUJeepSystem.PartData(jeep, part.Part);
            var fitted = part.Slot is not { } slotId ||
                         data?.Kind == CMUVehiclePartKind.Wheels ||
                         IsFitted(vehicle, vehicleSlots, slotId) ||
                         HeldFits(vehicle, vehicleSlots, slotId);
            var reachable = _jeepSystem.IsExposed(vehicle, jeep, part.Part) &&
                            (part.Slot is not { } kitSlot || !_jeepSystem.IsKitBlocked(vehicle, jeep, kitSlot));
            SetVisible((uid, sprite), "mask", !combat && fitted && reachable);
            sprite.RenderOrder = MaskOrder(part, jeep, vehicle, vehicleSprite, RenderDirection(xform));
            ApplyFrame((uid, sprite), xform);
        }
    }

    /// <summary>
    /// Shows a swinging part's state on the jeep and its overlay. Out of its off state the overlay
    /// draws it from the raisedKey layer, if given.
    /// </summary>
    /// <summary>
    /// A jeep's turret takes clicks and outlines on hover, so an ammo box can be used on it to load its
    /// gun. In combat mode clicks go through it, to fire.
    /// </summary>
    private void UpdateTurrets(bool combat)
    {
        var turrets = EntityQueryEnumerator<VehicleTurretVisualComponent>();
        while (turrets.MoveNext(out var uid, out var visual))
        {
            if (!TryGetEntity(visual.Turret, out var turret) ||
                !_turret.TryGetVehicle(turret.Value, out var vehicle) ||
                !HasComp<CMUJeepComponent>(vehicle))
            {
                continue;
            }

            if (combat)
            {
                RemComp<ClickableComponent>(uid);
                RemComp<InteractionOutlineComponent>(uid);
            }
            else
            {
                EnsureComp<ClickableComponent>(uid);
                EnsureComp<InteractionOutlineComponent>(uid);
            }
        }
    }

    /// <summary>
    /// How long a state's animation takes, played once: a swing's length.
    /// </summary>
    private static TimeSpan StateLength(SpriteComponent sprite, string state)
    {
        return sprite.BaseRSI is { } rsi && rsi.TryGetState(state, out var rsiState)
            ? TimeSpan.FromSeconds(rsiState.TotalDelay)
            : TimeSpan.Zero;
    }

    /// <summary>
    /// A point in a panel's opening swing just into its third frame: popped ajar.
    /// </summary>
    private static float AjarTime(SpriteComponent sprite, string state)
    {
        if (sprite.BaseRSI is not { } rsi || !rsi.TryGetState(state, out var rsiState))
            return 0f;

        var delays = rsiState.GetDelays();
        return delays.Take(2).Sum() + 0.01f;
    }

    private void Animate(
        Entity<SpriteComponent> jeep,
        Entity<SpriteComponent>? overlay,
        string key,
        Swing swing,
        bool on,
        TimeSpan length,
        string onState,
        string offState,
        string toOn,
        string toOff,
        string? raisedKey = null)
    {
        if (swing.Shown is { } shown && shown != on)
            swing.Start = _timing.RealTime;

        swing.Shown = on;
        var elapsed = swing.Start is { } start ? _timing.RealTime - start : length;
        if (elapsed >= length)
        {
            swing.Start = null;
            SetState(jeep, key, on ? onState : offState);
            if (overlay != null)
                SetState(overlay.Value, on ? raisedKey ?? key : key, OverlayState(on ? onState : offState));
            return;
        }

        // RSI states loop on their own, and game time can step back when the client re-applies
        // server state. The swing's frame comes from real time instead, which only goes forward,
        // and is held on the last frame, so it plays once per change.
        var state = on ? toOn : toOff;
        var time = (float) Math.Min(elapsed.TotalSeconds, length.TotalSeconds - 0.001);
        SetState(jeep, key, state, time);
        if (overlay != null)
            SetState(overlay.Value, raisedKey ?? key, OverlayState(state), time);
    }

    private static Swing SwingOf(Dictionary<string, Swing> swings, string key)
    {
        if (!swings.TryGetValue(key, out var swing))
            swings[key] = swing = new Swing();

        return swing;
    }

    /// <summary>
    /// A kit layer (states <c>name_suffix</c>, <c>name_open_suffix</c>, ..., each after the prefix of
    /// a look of its own, like a wheel set's road tyres) swinging with the panel it hangs on. Kit on no
    /// panel, or on one the vehicle lacks, stays put.
    /// </summary>
    private void AnimateKit(
        Entity<SpriteComponent> jeep,
        Entity<SpriteComponent>? overlay,
        Dictionary<string, Swing> swings,
        CMUJeepComponent comp,
        string name,
        string? panel,
        string suffix,
        string prefix = "")
    {
        var state = prefix + name;
        if (panel == null || !CMUJeepSystem.HasPart(comp, panel))
        {
            SetState(jeep, name, $"{state}_{suffix}");
            if (overlay != null)
                SetState(overlay.Value, name, $"{state}_overlay_{suffix}");

            return;
        }

        Animate(jeep, overlay, name, SwingOf(swings, name), CMUJeepSystem.GetPanelFlag(comp, panel),
            StateLength(jeep.Comp, $"{state}_opening_{suffix}"), $"{state}_open_{suffix}", $"{state}_{suffix}",
            $"{state}_opening_{suffix}", $"{state}_closing_{suffix}");
    }

    private static string OverlayState(string state)
    {
        return state.EndsWith("_0") || state.EndsWith("_1") ? $"{state[..^2]}_overlay{state[^2..]}" : $"{state}_overlay";
    }

    /// <summary>
    /// Whether a seat's door is open or off, showing more of the seat, all of which takes clicks and
    /// outlines (click_seat_<id>_open, seat_<id>_open_outline).
    /// </summary>
    private bool SeatOpened(EntityUid vehicle, CMUJeepComponent jeep, string part)
    {
        if (!part.StartsWith("seat_") || !TryComp(vehicle, out CMUVehicleSeatsComponent? seats))
            return false;

        var seat = seats.Seats.Find(s => $"seat_{s.Id}" == part);
        return seat?.Door is { } panel &&
               CMUJeepSystem.HasPart(jeep, panel) &&
               (CMUJeepSystem.GetPanelFlag(jeep, panel) || !_jeepSystem.IsPanelFitted(vehicle, jeep, panel));
    }

    /// <summary>
    /// Whether kit hangs on a panel that stands open, showing its swung look.
    /// </summary>
    private static bool HostOpen(CMUVehiclePartData? data, CMUJeepComponent jeep)
    {
        return data?.Host is { } host && CMUJeepSystem.GetPanelFlag(jeep, host);
    }

    private static string MaskState(CMUVehiclePartComponent part, CMUJeepComponent jeep, bool seatOpened)
    {
        var id = part.Part;
        var data = CMUJeepSystem.PartData(jeep, id);
        return data?.Kind switch
        {
            CMUVehiclePartKind.Panel => CMUJeepSystem.GetPanelFlag(jeep, id) ? $"click_{id}_open" : $"click_{id}_closed",
            CMUVehiclePartKind.Windshield => jeep.WindshieldDown ? $"click_{id}_down" : $"click_{id}_up",
            _ when HostOpen(data, jeep) || seatOpened => $"click_{id}_open",
            _ => $"click_{id}",
        };
    }

    /// <summary>
    /// A part mask's render order, which takes the click where masks overlap: the order the vehicle,
    /// then its overlay over the riders, draws the part's layers in, a raised panel from its raised
    /// layer. The seats are under everything.
    /// </summary>
    private uint MaskOrder(CMUVehiclePartComponent part, CMUJeepComponent jeep, EntityUid vehicle, SpriteComponent vehicleSprite, Direction direction)
    {
        var data = CMUJeepSystem.PartData(jeep, part.Part);
        var raised = data?.RaisedLayer is { } raisedLayer && CMUJeepSystem.GetPanelFlag(jeep, part.Part) ? raisedLayer : null;
        var count = vehicleSprite.AllLayers.Count();
        if (OverRiders(vehicle, data, part.Part).Contains(direction) &&
            TryComp(vehicle, out CMUVehicleOverlayComponent? overlay) &&
            TryComp(overlay.Overlay, out SpriteComponent? overlaySprite) &&
            _sprite.LayerMapTryGet((overlay.Overlay.Value, overlaySprite), raised ?? part.Part, out var overIndex, false))
        {
            return (uint) (2 + count + overIndex);
        }

        var found = data?.Kind == CMUVehiclePartKind.Wheels
            ? _sprite.LayerMapTryGet((vehicle, vehicleSprite), VehicleWheelLayers.Wheels, out var index, false)
            : _sprite.LayerMapTryGet((vehicle, vehicleSprite), part.Part, out index, false);
        return found ? (uint) (2 + index) : 1;
    }

    private List<Direction> OverRiders(EntityUid vehicle, CMUVehiclePartData? data, string part)
    {
        if (data != null)
            return data.OverRiders;

        return TryComp(vehicle, out CMUJeepLampsComponent? lamps) && lamps.Lamps.Find(l => l.Id == part) is { } lamp
            ? lamp.OverRiders
            : NoDirections;
    }

    private static readonly List<Direction> NoDirections = new();

    private static string OutlineState(CMUVehiclePartComponent part, CMUJeepComponent jeep, bool seatOpened)
    {
        var id = part.Part;
        var data = CMUJeepSystem.PartData(jeep, id);
        return data?.Kind switch
        {
            CMUVehiclePartKind.Panel => CMUJeepSystem.GetPanelFlag(jeep, id) ? $"{id}_open_outline" : $"{id}_outline",
            CMUVehiclePartKind.Windshield => jeep.WindshieldDown ? $"{id}_down_outline" : $"{id}_up_outline",
            _ when HostOpen(data, jeep) || seatOpened => $"{id}_open_outline",
            _ => $"{id}_outline",
        };
    }

    private void SetState(Entity<SpriteComponent> ent, string key, string state, float? animationTime = null)
    {
        if (!_sprite.LayerMapTryGet(ent.AsNullable(), key, out var index, false))
            return;

        if (_sprite.LayerGetRsiState(ent.AsNullable(), index) != state)
            _sprite.LayerSetRsiState(ent.AsNullable(), index, state);

        // A driven frame must not also advance on its own.
        _sprite.LayerSetAutoAnimated(ent.AsNullable(), index, animationTime == null);
        if (animationTime is { } time)
            _sprite.LayerSetAnimationTime(ent.AsNullable(), index, time);
    }

    private bool IsFitted(EntityUid vehicle, ItemSlotsComponent? slots, string slotId)
    {
        return slots != null &&
               _itemSlots.TryGetSlot((vehicle, slots), slotId, out var slot) &&
               slot.HasItem;
    }

    /// <summary>
    /// Whether the local player's held item would go into a vehicle's empty slot.
    /// </summary>
    private bool HeldFits(EntityUid vehicle, ItemSlotsComponent? slots, string slotId)
    {
        return slots != null &&
               _player.LocalEntity is { } user &&
               _hands.GetActiveItem(user) is { } held &&
               _itemSlots.TryGetSlot((vehicle, slots), slotId, out var slot) &&
               !slot.HasItem &&
               _itemSlots.CanInsert(vehicle, slot, held, null);
    }

    /// <summary>
    /// A shut panel's state: the hood crumpled with the front, for as far as the front is crumpled.
    /// </summary>
    private string ClosedState(EntityUid uid, string panel)
    {
        return TryComp(uid, out CMUVehicleFrontDamageComponent? front) &&
               front.ClosedPanel == panel &&
               front.ClosedHoodState is { } dented &&
               _frontDamage.GetLevel((uid, front)) is var level and > 0
            ? dented + level
            : $"{panel}_closed";
    }

    private void SetVisible(Entity<SpriteComponent> ent, string key, bool visible)
    {
        if (_sprite.LayerMapTryGet(ent.AsNullable(), key, out var index, false))
            _sprite.LayerSetVisible(ent.AsNullable(), index, visible);
    }

    /// <summary>
    /// Draws a part's mask on the jeep's own cardinal frame, centred on the jeep and turned by the
    /// same leftover angle as the bodywork, which a jeep can also rest at. The engine's click test
    /// follows a no-rotation sprite's layer rotation, so clicks and hover land on the part drawn there.
    /// </summary>
    private Direction RenderDirection(TransformComponent xform)
    {
        return VehicleTurretDirectionHelpers.GetRenderAlignedCardinalDir(_transform.GetWorldRotation(xform) + _eye.CurrentEye.Rotation);
    }

    private void ApplyFrame(Entity<SpriteComponent> ent, TransformComponent xform)
    {
        var sprite = ent.Comp;
        var eyeRotation = _eye.CurrentEye.Rotation;
        var worldRotation = _transform.GetWorldRotation(xform);
        var direction = RenderDirection(xform);
        var leftover = worldRotation + eyeRotation - direction.ToAngle();

        sprite.EnableDirectionOverride = true;
        sprite.DirectionOverride = direction;
        sprite.NoRotation = true;
        _sprite.SetGranularLayersRendering(ent.AsNullable(), false);

        var toJeep = eyeRotation.RotateVec(-worldRotation.RotateVec(xform.LocalPosition));
        for (var i = 0; i < sprite.AllLayers.Count(); i++)
        {
            _sprite.LayerSetRotation(ent.AsNullable(), i, leftover);
            _sprite.LayerSetOffset(ent.AsNullable(), i, toJeep);
        }
    }

    private bool InReach(EntityUid part)
    {
        return _player.LocalEntity is { } user && _interaction.InRangeUnobstructed(user, part);
    }

    private EntityUid? GetHovered()
    {
        if (_state.CurrentState is not GameplayStateBase gameplay)
            return null;

        return gameplay.GetClickedEntity(_eye.PixelToMap(_input.MouseScreenPosition));
    }
}
