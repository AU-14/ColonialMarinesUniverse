using System.Linq;
using Content.Client.CombatMode;
using Content.Client.Gameplay;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.Vehicle.Jeep;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// Draws an open jeep's hood, windshield, doors, tailgate, fuel door and stowed kit from its state
/// with their swing animations (the kit hung on a door or the tailgate swings with it), keeps its
/// clickable part masks on the jeep's frame and outlines the part under the mouse.
/// </summary>
public sealed class CMUJeepVisualSystem : EntitySystem
{
    [Dependency] private CombatModeSystem _combat = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private CMUJeepSystem _jeepSystem = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IStateManager _state = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    // Frames times frame delay of the generated swing states.
    private static readonly TimeSpan HoodSwing = TimeSpan.FromSeconds(7 * 0.07);
    private static readonly TimeSpan WindshieldSwing = TimeSpan.FromSeconds(5 * 0.07);
    private static readonly TimeSpan FuelDoorSwing = TimeSpan.FromSeconds(4 * 0.07);
    private static readonly TimeSpan PanelSwing = TimeSpan.FromSeconds(5 * 0.07);

    private static readonly string[] Panels = [CMUJeepSystem.DriverDoor, CMUJeepSystem.PassengerDoor, CMUJeepSystem.Tailgate];

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

            Animate((uid, sprite), overlay, "hood", SwingOf(swings, "hood"), jeep.HoodOpen, HoodSwing,
                "hood_open", "hood_closed", "hood_opening", "hood_closing");
            var glass = jeep.WindshieldDamaged ? "1" : "0";
            Animate((uid, sprite), overlay, "windshield", SwingOf(swings, "windshield"), jeep.WindshieldDown, WindshieldSwing,
                $"windshield_down_{glass}", $"windshield_up_{glass}", $"windshield_folding_{glass}", $"windshield_raising_{glass}");

            foreach (var panel in Panels)
            {
                if (CMUJeepSystem.HasPart(jeep, panel))
                {
                    Animate((uid, sprite), overlay, panel, SwingOf(swings, panel), CMUJeepSystem.GetPanelFlag(jeep, panel),
                        PanelSwing, $"{panel}_open", $"{panel}_closed", $"{panel}_opening", $"{panel}_closing");
                }
            }

            var lamps = jeep.HeadlightsBroken ? "1" : "0";
            SetState((uid, sprite), "headlights", $"headlights_{lamps}");

            // Kit hung on a panel swings with it: the shovel and the axe on the driver's door, the
            // spare and the jerry can on the tailgate.
            var can = jeep.JerryCanLeaking ? "1" : "0";
            AnimateKit((uid, sprite), overlay, swings, jeep, "shovel", CMUJeepSystem.DriverDoor, "0");
            AnimateKit((uid, sprite), overlay, swings, jeep, "axe", CMUJeepSystem.DriverDoor, "0");
            AnimateKit((uid, sprite), overlay, swings, jeep, "spare", CMUJeepSystem.Tailgate, "0");
            AnimateKit((uid, sprite), overlay, swings, jeep, "jerrycan", CMUJeepSystem.Tailgate, can);
            var engine = jeep.EngineIntegrity / jeep.EngineMaxIntegrity;
            SetState((uid, sprite), "engine", engine < jeep.EngineSmokeFraction ? "engine_1" : "engine_0");
            if (overlay != null)
            {
                SetState(overlay.Value, "headlights", $"headlights_overlay_{lamps}");
                SetState(overlay.Value, "smoke", engine < jeep.EngineHeavySmokeFraction ? "engine_smoke_1" : "engine_smoke_0");
                SetVisible(overlay.Value, "smoke", engine < jeep.EngineSmokeFraction);
            }
            Animate((uid, sprite), overlay, "fuel_door", SwingOf(swings, "fuel_door"), jeep.FuelDoorOpen, FuelDoorSwing,
                "fuel_door_open", "fuel_door_closed", "fuel_door_opening", "fuel_door_closing");

            TryComp(uid, out ItemSlotsComponent? slots);
            foreach (var part in jeep.Parts)
            {
                if (part.Slot == null || part.Id == "wheels")
                    continue;

                var fitted = IsFitted(uid, slots, part.Slot);
                SetVisible((uid, sprite), part.Id, fitted);
                if (overlay != null)
                    SetVisible(overlay.Value, part.Id, fitted);
            }

            // The beam needs intact headlights; the switch still lights the tail and marker lamps.
            if (overlay != null && (jeep.HeadlightsBroken || !IsFitted(uid, slots, CMUJeepSystem.HeadlightsSlot)))
                SetVisible(overlay.Value, "headlights_on", false);

            if (TryComp(uid, out CMUVehicleCargoComponent? cargo) && overlay != null)
                SetVisible(overlay.Value, "straps", cargo.Crate != null && cargo.Secured);

            if (overlay != null)
            {
                var outline = hovered is { } h &&
                              TryComp(h, out CMUVehiclePartComponent? hoveredPart) &&
                              hoveredPart.Vehicle == uid
                    ? OutlineState(hoveredPart, jeep)
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

            SetState((uid, sprite), "mask", MaskState(part, jeep));

            // A hidden mask drops out of the click test, so in combat mode attacks and aimed shots land
            // on the jeep itself, and an empty slot is refitted through the jeep's own item slots.
            var fitted = part.Slot is not { } slotId ||
                         part.Part == "wheels" ||
                         (TryComp(vehicle, out ItemSlotsComponent? vehicleSlots) && IsFitted(vehicle, vehicleSlots, slotId));
            var reachable = part.Part switch
            {
                "engine" => _jeepSystem.IsHoodOpen(vehicle, jeep),
                _ => part.Slot is not { } kitSlot || !_jeepSystem.IsKitBlocked(vehicle, jeep, kitSlot),
            };
            SetVisible((uid, sprite), "mask", !combat && fitted && reachable);
            sprite.RenderOrder = 1;
            ApplyFrame((uid, sprite), xform);
        }
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
        string toOff)
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
                SetState(overlay.Value, key, OverlayState(on ? onState : offState));
            return;
        }

        // RSI states loop on their own, and game time can step back when the client re-applies
        // server state. The swing's frame comes from real time instead, which only goes forward,
        // and is held on the last frame, so it plays once per change.
        var state = on ? toOn : toOff;
        var time = (float) Math.Min(elapsed.TotalSeconds, length.TotalSeconds - 0.001);
        SetState(jeep, key, state, time);
        if (overlay != null)
            SetState(overlay.Value, key, OverlayState(state), time);
    }

    private static Swing SwingOf(Dictionary<string, Swing> swings, string key)
    {
        if (!swings.TryGetValue(key, out var swing))
            swings[key] = swing = new Swing();

        return swing;
    }

    /// <summary>
    /// A hung kit layer (states <c>name_suffix</c>, <c>name_open_suffix</c>, ...) swinging with the
    /// panel it hangs on. A jeep without that panel keeps it fixed.
    /// </summary>
    private void AnimateKit(
        Entity<SpriteComponent> jeep,
        Entity<SpriteComponent>? overlay,
        Dictionary<string, Swing> swings,
        CMUJeepComponent comp,
        string name,
        string panel,
        string suffix)
    {
        if (!CMUJeepSystem.HasPart(comp, panel))
        {
            SetState(jeep, name, $"{name}_{suffix}");
            if (overlay != null)
                SetState(overlay.Value, name, $"{name}_overlay_{suffix}");

            return;
        }

        Animate(jeep, overlay, name, SwingOf(swings, name), CMUJeepSystem.GetPanelFlag(comp, panel), PanelSwing,
            $"{name}_open_{suffix}", $"{name}_{suffix}", $"{name}_opening_{suffix}", $"{name}_closing_{suffix}");
    }

    private static string OverlayState(string state)
    {
        return state.EndsWith("_0") || state.EndsWith("_1") ? $"{state[..^2]}_overlay{state[^2..]}" : $"{state}_overlay";
    }

    private static string MaskState(CMUVehiclePartComponent part, CMUJeepComponent jeep)
    {
        return part.Part switch
        {
            "hood" => jeep.HoodOpen ? "click_hood_open" : "click_hood_closed",
            "windshield" => jeep.WindshieldDown ? "click_windshield_down" : "click_windshield_up",
            "fuel_door" => jeep.FuelDoorOpen ? "click_fuel_door_open" : "click_fuel_door_closed",
            CMUJeepSystem.DriverDoor or CMUJeepSystem.PassengerDoor or CMUJeepSystem.Tailgate =>
                CMUJeepSystem.GetPanelFlag(jeep, part.Part) ? $"click_{part.Part}_open" : $"click_{part.Part}_closed",
            "shovel" or "axe" when jeep.DriverDoorOpen => $"click_{part.Part}_open",
            var id => $"click_{id}",
        };
    }

    private static string OutlineState(CMUVehiclePartComponent part, CMUJeepComponent jeep)
    {
        return part.Part switch
        {
            "hood" => jeep.HoodOpen ? "hood_open_outline" : "hood_outline",
            "windshield" => jeep.WindshieldDown ? "windshield_down_outline" : "windshield_up_outline",
            "fuel_door" => jeep.FuelDoorOpen ? "fuel_door_open_outline" : "fuel_door_outline",
            CMUJeepSystem.DriverDoor or CMUJeepSystem.PassengerDoor or CMUJeepSystem.Tailgate =>
                CMUJeepSystem.GetPanelFlag(jeep, part.Part) ? $"{part.Part}_open_outline" : $"{part.Part}_outline",
            "shovel" or "axe" when jeep.DriverDoorOpen => $"{part.Part}_open_outline",
            var id => $"{id}_outline",
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
    private void ApplyFrame(Entity<SpriteComponent> ent, TransformComponent xform)
    {
        var sprite = ent.Comp;
        var eyeRotation = _eye.CurrentEye.Rotation;
        var worldRotation = _transform.GetWorldRotation(xform);
        var direction = VehicleTurretDirectionHelpers.GetRenderAlignedCardinalDir(worldRotation + eyeRotation);
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
