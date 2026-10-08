using System.Linq;
using Content.Client.Gameplay;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.Vehicle.Jeep;
using Content.Shared.Containers.ItemSlots;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.State;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Vehicle;

/// <summary>
/// Draws an open jeep's hood, windshield, fuel door and stowed kit from its state with their swing
/// animations, keeps its clickable part masks on the jeep's frame and outlines the part under the
/// mouse.
/// </summary>
public sealed class CMUJeepVisualSystem : EntitySystem
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IStateManager _state = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    // Frames times frame delay of the generated swing states.
    private static readonly TimeSpan HoodSwing = TimeSpan.FromSeconds(7 * 0.07);
    private static readonly TimeSpan WindshieldSwing = TimeSpan.FromSeconds(5 * 0.07);
    private static readonly TimeSpan FuelDoorSwing = TimeSpan.FromSeconds(4 * 0.07);

    private readonly Dictionary<EntityUid, (Swing Hood, Swing Windshield, Swing FuelDoor)> _swings = new();

    private sealed class Swing
    {
        public bool? Shown;
        public TimeSpan Until;
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
            Entity<SpriteComponent>? overlay = null;
            if (TryComp(uid, out CMUVehicleOverlayComponent? over) &&
                TryComp(over.Overlay, out SpriteComponent? overSprite))
            {
                overlay = (over.Overlay.Value, overSprite);
            }

            if (!_swings.TryGetValue(uid, out var swings))
                _swings[uid] = swings = (new Swing(), new Swing(), new Swing());

            Animate((uid, sprite), overlay, "hood", swings.Hood, jeep.HoodOpen, HoodSwing,
                "hood_open", "hood_closed", "hood_opening", "hood_closing");
            var glass = jeep.WindshieldDamaged ? "1" : "0";
            Animate((uid, sprite), overlay, "windshield", swings.Windshield, jeep.WindshieldDown, WindshieldSwing,
                $"windshield_down_{glass}", $"windshield_up_{glass}", $"windshield_folding_{glass}", $"windshield_raising_{glass}");

            var lamps = jeep.HeadlightsBroken ? "1" : "0";
            SetState((uid, sprite), "headlights", $"headlights_{lamps}");
            var can = jeep.JerryCanLeaking ? "1" : "0";
            SetState((uid, sprite), "jerrycan", $"jerrycan_{can}");
            var engine = jeep.EngineIntegrity / jeep.EngineMaxIntegrity;
            SetState((uid, sprite), "engine", engine < jeep.EngineSmokeFraction ? "engine_1" : "engine_0");
            if (overlay != null)
            {
                SetState(overlay.Value, "headlights", $"headlights_overlay_{lamps}");
                SetState(overlay.Value, "jerrycan", $"jerrycan_overlay_{can}");
                SetState(overlay.Value, "smoke", engine < jeep.EngineHeavySmokeFraction ? "engine_smoke_1" : "engine_smoke_0");
                SetVisible(overlay.Value, "smoke", engine < jeep.EngineSmokeFraction);
            }
            Animate((uid, sprite), overlay, "fuel_door", swings.FuelDoor, jeep.FuelDoorOpen, FuelDoorSwing,
                "fuel_door_open", "fuel_door_closed", "fuel_door_opening", "fuel_door_closing");

            foreach (var part in jeep.Parts)
            {
                if (part.Slot == null || part.Id == "wheels")
                    continue;

                var fitted = _itemSlots.TryGetSlot(uid, part.Slot, out var slot) && slot.HasItem;
                SetVisible((uid, sprite), part.Id, fitted);
                if (overlay != null)
                    SetVisible(overlay.Value, part.Id, fitted);
            }

            // The beam needs intact headlights; the switch still lights the tail and marker lamps.
            if (overlay != null && (jeep.HeadlightsBroken || !_itemSlots.TryGetSlot(uid, CMUJeepSystem.HeadlightsSlot, out var lights) || !lights.HasItem))
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
                    SetState(overlay.Value, "hover", outline);

                SetVisible(overlay.Value, "hover", outline != null);
            }
        }

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
            SetVisible((uid, sprite), "mask", part.Part != "engine" || jeep.HoodOpen);
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
        var restart = swing.Shown is { } shown && shown != on;
        if (restart)
            swing.Until = _timing.CurTime + length;

        swing.Shown = on;
        var state = _timing.CurTime < swing.Until ? on ? toOn : toOff : on ? onState : offState;

        SetState(jeep, key, state, restart);
        if (overlay != null)
            SetState(overlay.Value, key, OverlayState(state), restart);
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
            var id => $"{id}_outline",
        };
    }

    private void SetState(Entity<SpriteComponent> ent, string key, string state, bool restart = false)
    {
        if (!_sprite.LayerMapTryGet(ent.AsNullable(), key, out var index, false))
            return;

        if (_sprite.LayerGetRsiState(ent.AsNullable(), index) != state)
            _sprite.LayerSetRsiState(ent.AsNullable(), index, state);

        if (restart)
            _sprite.LayerSetAnimationTime(ent.AsNullable(), index, 0f);
    }

    private void SetVisible(Entity<SpriteComponent> ent, string key, bool visible)
    {
        if (_sprite.LayerMapTryGet(ent.AsNullable(), key, out var index, false))
            _sprite.LayerSetVisible(ent.AsNullable(), index, visible);
    }

    /// <summary>
    /// Draws a part's mask on the jeep's own cardinal frame, centred on the jeep. Masks are invisible,
    /// so they skip the residual turn the bodywork shows mid-rotation; drawn unrotated, the engine's
    /// click test matches them in every direction.
    /// </summary>
    private void ApplyFrame(Entity<SpriteComponent> ent, TransformComponent xform)
    {
        var sprite = ent.Comp;
        var eyeRotation = _eye.CurrentEye.Rotation;
        var worldRotation = _transform.GetWorldRotation(xform);
        var direction = VehicleTurretDirectionHelpers.GetRenderAlignedCardinalDir(worldRotation + eyeRotation);

        sprite.EnableDirectionOverride = true;
        sprite.DirectionOverride = direction;
        sprite.NoRotation = true;
        _sprite.SetGranularLayersRendering(ent.AsNullable(), false);

        var toJeep = eyeRotation.RotateVec(-worldRotation.RotateVec(xform.LocalPosition));
        for (var i = 0; i < sprite.AllLayers.Count(); i++)
        {
            _sprite.LayerSetRotation(ent.AsNullable(), i, Angle.Zero);
            _sprite.LayerSetOffset(ent.AsNullable(), i, toJeep);
        }
    }

    private EntityUid? GetHovered()
    {
        if (_state.CurrentState is not GameplayStateBase gameplay)
            return null;

        return gameplay.GetClickedEntity(_eye.PixelToMap(_input.MouseScreenPosition));
    }
}
