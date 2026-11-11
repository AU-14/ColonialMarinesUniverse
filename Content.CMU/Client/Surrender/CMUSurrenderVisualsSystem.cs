using Content.Client.Body;
using Content.Client.DisplacementMap;
using Content.Client.Inventory;
using Content.Shared.Body;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.CMU14.Surrender;
using Content.Shared.DisplacementMap;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Storage;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Surrender;

[RegisterComponent]
public sealed partial class CMUSurrenderFacingComponent : Component
{
    public bool OriginalOverride;
    public Direction OriginalDirection;
    public Direction VerticalDirection = Direction.South;
}

public sealed class CMUSurrenderVisualsSystem : EntitySystem
{
    [Dependency] private BodySystem _body = default!;
    [Dependency] private DisplacementMapSystem _displacement = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IEyeManager _eye = default!;

    private static readonly ProtoId<DisplacementDataPrototype> LimbsMap = "CMUSurrenderLimbs";
    private static readonly ProtoId<DisplacementDataPrototype> TorsoMap = "CMUSurrenderTorso";
    private static readonly ProtoId<DisplacementDataPrototype> ForeLimbsMap = "CMUSurrenderForegroundLimbs";
    private static readonly ProtoId<DisplacementDataPrototype> GearFillMap = "CMUSurrenderWaistGear";
    private static readonly ProtoId<DisplacementDataPrototype>[] ClothingMaps =
    [
        "CMUSurrenderClothingNone", "CMUSurrenderClothingLeft", "CMUSurrenderClothingRight", "CMUSurrenderClothing",
    ];
    private static readonly ProtoId<DisplacementDataPrototype>[] ForegroundClothingMaps =
    [
        "CMUSurrenderForegroundClothingNone", "CMUSurrenderForegroundClothingLeft", "CMUSurrenderForegroundClothingRight", "CMUSurrenderForegroundClothing",
    ];
    private static readonly ProtoId<DisplacementDataPrototype>[] ArmorMaps =
    [
        "CMUSurrenderArmorNone", "CMUSurrenderArmorLeft", "CMUSurrenderArmorRight", "CMUSurrenderArmor",
    ];
    private static readonly ProtoId<DisplacementDataPrototype>[] ForegroundArmorMaps =
    [
        "CMUSurrenderForegroundArmorNone", "CMUSurrenderForegroundArmorLeft", "CMUSurrenderForegroundArmorRight", "CMUSurrenderForegroundArmor",
    ];
    private static readonly ProtoId<DisplacementDataPrototype>[] LimbsMaps =
    [
        "CMUSurrenderLimbsNone", "CMUSurrenderLimbsLeft", "CMUSurrenderLimbsRight", "CMUSurrenderLimbs",
    ];
    private static readonly ProtoId<DisplacementDataPrototype>[] ForegroundLimbsMaps =
    [
        "CMUSurrenderForegroundLimbsNone", "CMUSurrenderForegroundLimbsLeft", "CMUSurrenderForegroundLimbsRight", "CMUSurrenderForegroundLimbs",
    ];

    private static readonly HumanoidVisualLayers[] ArmLayers =
    [
        HumanoidVisualLayers.LArm,
        HumanoidVisualLayers.RArm,
        HumanoidVisualLayers.LHand,
        HumanoidVisualLayers.RHand,
    ];
    private static readonly string[] ClothingSlots = ["jumpsuit", "outerClothing", "gloves"];
    private static readonly string[] StorageSlots = ["belt", "back"];

    private readonly HashSet<EntityUid> _pending = new();
    private readonly Dictionary<EntityUid, List<PosedLayer>> _posed = new();

    private sealed record PosedLayer(
        string Alias,
        SpriteComponent.Layer Layer,
        string DisplacementKey,
        SpriteComponent.Layer DisplacementLayer,
        ShaderInstance? OriginalShader,
        ProtoId<ShaderPrototype>? OriginalPrototype,
        ShaderInstance? PoseShader,
        bool Temporary);

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUSurrenderComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CMUSurrenderComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CMUSurrenderComponent, VisualBodySpriteRefreshEvent>(OnBodyRefresh);
        SubscribeLocalEvent<CMUSurrenderComponent, DidUnequipEvent>(OnUnequip);
        SubscribeLocalEvent<ClothingComponent, EquipmentVisualsUpdatedEvent>(OnEquipmentRefresh);
    }

    private void OnStartup(Entity<CMUSurrenderComponent> ent, ref ComponentStartup args)
    {
        _pending.Add(ent);
    }

    private void OnShutdown(Entity<CMUSurrenderComponent> ent, ref ComponentShutdown args)
    {
        _pending.Remove(ent);
        Restore(ent);
        RestoreFacing(ent);
    }

    private void OnBodyRefresh(Entity<CMUSurrenderComponent> ent, ref VisualBodySpriteRefreshEvent args)
    {
        _pending.Add(ent);
    }

    private void OnEquipmentRefresh(Entity<ClothingComponent> ent, ref EquipmentVisualsUpdatedEvent args)
    {
        if (HasComp<CMUSurrenderComponent>(args.Equipee))
            _pending.Add(args.Equipee);
    }

    private void OnUnequip(Entity<CMUSurrenderComponent> ent, ref DidUnequipEvent args)
    {
        _pending.Add(ent);
    }

    public override void FrameUpdate(float frameTime)
    {
        // Markings can be replaced without a body-refresh event (e.g. applying a profile locally).
        // Their removal also deletes our alias, but leaves the displacement layer pointing at it.
        foreach (var (uid, layers) in _posed)
        {
            if (_pending.Contains(uid))
                continue;

            if (!TryComp<SpriteComponent>(uid, out var sprite))
            {
                _pending.Add(uid);
                continue;
            }

            foreach (var entry in layers)
            {
                if (!_sprite.TryGetLayer((uid, sprite), entry.Alias, out var layer, false)
                    || layer != entry.Layer
                    || layer.Shader != entry.PoseShader
                    || !_sprite.TryGetLayer((uid, sprite), entry.DisplacementKey, out var displacement, false)
                    || displacement != entry.DisplacementLayer)
                {
                    _pending.Add(uid);
                    break;
                }
            }
        }

        foreach (var uid in _pending)
        {
            Restore(uid);
            if (HasComp<CMUSurrenderComponent>(uid) && TryComp<SpriteComponent>(uid, out var sprite))
                Apply((uid, sprite));
        }
        _pending.Clear();
        var query = EntityQueryEnumerator<CMUSurrenderFacingComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var facing, out var sprite))
            UpdateFacing(uid, sprite, facing);
    }

    private void Apply(Entity<SpriteComponent> ent)
    {
        if (!TryComp<CMUSurrenderFacingComponent>(ent.Owner, out var facing))
        {
            facing = AddComp<CMUSurrenderFacingComponent>(ent.Owner);
            facing.OriginalOverride = ent.Comp.EnableDirectionOverride;
            facing.OriginalDirection = ent.Comp.DirectionOverride;
        }
        UpdateFacing(ent.Owner, ent.Comp, facing);
        var arms = 0;
        var hands = 0;
        foreach (var organ in _body.EnumerateOrgans<VisualOrganComponent>(ent.Owner))
        {
            if (organ.Comp2.Layer is not HumanoidVisualLayers part)
                continue;
            switch (part)
            {
                case HumanoidVisualLayers.LArm: arms |= 1; break;
                case HumanoidVisualLayers.RArm: arms |= 2; break;
                case HumanoidVisualLayers.LHand: hands |= 1; break;
                case HumanoidVisualLayers.RHand: hands |= 2; break;
            }
        }

        var singleArm = arms is 1 or 2;
        var layers = new List<PosedLayer>();
        _posed[ent] = layers;
        PoseLayer(ent, HumanoidVisualLayers.Chest, ProtoMan.Index(TorsoMap).Displacement, layers);
        var limbs = ProtoMan.Index(LimbsMap).Displacement;
        foreach (var key in ArmLayers)
        {
            if (HasLimb(key, arms, hands))
                PoseLayer(ent, key, limbs, layers, ForeLimbsMap,
                    fallbackForeground: singleArm ? (ProtoId<DisplacementDataPrototype>?) ForegroundLimbsMaps[arms] : null);
        }

        var clothing = ProtoMan.Index(ClothingMaps[arms]).Displacement;
        var gearFill = ProtoMan.Index(GearFillMap).Displacement;

        foreach (var organ in _body.EnumerateOrgans<VisualOrganMarkingsComponent>(ent.Owner))
        {
            foreach (var marking in organ.Comp2.AppliedMarkings)
            {
                if (!ProtoMan.TryIndex<MarkingPrototype>(marking.MarkingId, out var proto)
                    || (!IsArmLayer(proto.BodyPart)
                        && proto.BodyPart is not (HumanoidVisualLayers.UndergarmentTop or HumanoidVisualLayers.UndergarmentBottom)))
                {
                    continue;
                }

                if (IsArmLayer(proto.BodyPart) && !HasLimb(proto.BodyPart, arms, hands))
                    continue;

                var undershirt = proto.BodyPart == HumanoidVisualLayers.UndergarmentTop;
                foreach (var specifier in proto.Sprites)
                {
                    if (specifier is SpriteSpecifier.Rsi rsi)
                    {
                        var key = $"{proto.ID}-{rsi.RsiState}";
                        if (proto.BodyPart is HumanoidVisualLayers.UndergarmentTop or HumanoidVisualLayers.UndergarmentBottom)
                            FillHandCutout(ent, key, gearFill, layers, coverOutline: true);
                        if (proto.BodyPart != HumanoidVisualLayers.UndergarmentBottom)
                            PoseLayer(ent, key, undershirt ? clothing : limbs,
                                layers, undershirt ? ForegroundClothingMaps[arms] : ForeLimbsMap,
                                fallbackForeground: singleArm
                                    ? (ProtoId<DisplacementDataPrototype>?) (undershirt ? ForegroundClothingMaps[arms] : ForegroundLimbsMaps[arms])
                                    : null);
                    }
                }
            }
        }

        if (!TryComp<InventorySlotsComponent>(ent, out var inventory))
            return;

        foreach (var slot in ClothingSlots)
        {
            if (!inventory.VisualLayerKeys.TryGetValue(slot, out var keys))
                continue;

            var map = slot switch
            {
                "gloves" => LimbsMaps[hands],
                "outerClothing" => ArmorMaps[arms],
                _ => ClothingMaps[arms],
            };
            var foreground = slot switch
            {
                "gloves" => ForegroundLimbsMaps[hands],
                "outerClothing" => ForegroundArmorMaps[arms],
                _ => ForegroundClothingMaps[arms],
            };
            var fallback = slot == "gloves" ? ForegroundLimbsMaps[hands & arms] : foreground;
            foreach (var key in keys)
            {
                if (slot == "jumpsuit")
                    FillHandCutout(ent, key, gearFill, layers, coverOutline: true);
                PoseLayer(ent, key, ProtoMan.Index(map).Displacement, layers, foreground,
                    fallbackForeground: singleArm ? (ProtoId<DisplacementDataPrototype>?) fallback : null);
            }
        }

        foreach (var slot in StorageSlots)
        {
            if (!_inventory.TryGetSlotEntity(ent.Owner, slot, out var item)
                || !HasComp<StorageComponent>(item)
                || !inventory.VisualLayerKeys.TryGetValue(slot, out var keys))
            {
                continue;
            }

            foreach (var key in keys)
                FillHandCutout(ent, key, gearFill, layers);
        }
    }

    private void FillHandCutout(Entity<SpriteComponent> ent, string key, DisplacementData map,
        List<PosedLayer> layers, bool coverOutline = false)
    {
        if (!_sprite.LayerMapTryGet(ent.AsNullable(), key, out var index, false)
            || !_sprite.TryGetLayer(ent.AsNullable(), index, out var layer, false)
            || layer.CopyToShaderParameters != null
            || _sprite.LayerGetEffectiveRsi(ent.AsNullable(), index) is not { } rsi
            || rsi.Size != new Vector2i(32, 32))
        {
            return;
        }

        var copy = new SpriteComponent.Layer(layer, ent.Comp);
        var copyKey = $"cmu-surrender-fill-{key}";
        var copyIndex = _sprite.AddLayer(ent.AsNullable(), copy, coverOutline ? index + 1 : index);
        _sprite.LayerMapSet(ent.AsNullable(), copyKey, copyIndex);
        PoseLayer(ent, copyKey, map, layers, temporary: true);
    }

    private void PoseLayer(Entity<SpriteComponent> ent, object key, DisplacementData map, List<PosedLayer> layers, ProtoId<DisplacementDataPrototype>? foreground = null, bool temporary = false, ProtoId<DisplacementDataPrototype>? fallbackForeground = null)
    {
        var index = 0;
        var mapped = key switch
        {
            Enum enumKey => _sprite.LayerMapTryGet(ent.AsNullable(), enumKey, out index, false),
            string stringKey => _sprite.LayerMapTryGet(ent.AsNullable(), stringKey, out index, false),
            _ => false,
        };
        if (!mapped
            || !_sprite.TryGetLayer(ent.AsNullable(), index, out var layer, false)
            || layer.CopyToShaderParameters != null
            || _sprite.LayerGetEffectiveRsi(ent.AsNullable(), index) is not { } rsi
            || rsi.Size != new Vector2i(32, 32))
        {
            return;
        }

        foreach (var existing in layers)
        {
            if (existing.Layer == layer)
                return;
        }

        if (foreground is { } foregroundMap)
        {

            var copy = new SpriteComponent.Layer(layer, ent.Comp);
            var copyKey = $"cmu-surrender-front-{key}";
            var copyIndex = _sprite.AddLayer(ent.AsNullable(), copy);
            _sprite.LayerMapSet(ent.AsNullable(), copyKey, copyIndex);
            PoseLayer(ent, copyKey, ProtoMan.Index(foregroundMap).Displacement, layers, temporary: true);
        }

        if (fallbackForeground is { } fallbackMap)
        {

            var copy = new SpriteComponent.Layer(layer, ent.Comp);
            var copyKey = $"cmu-surrender-far-{key}";
            var copyIndex = _sprite.AddLayer(ent.AsNullable(), copy);
            _sprite.LayerMapSet(ent.AsNullable(), copyKey, copyIndex);
            _sprite.LayerSetDirOffset(copy, SpriteComponent.DirectionOffset.Flip);
            _sprite.LayerSetScale(copy, new System.Numerics.Vector2(-copy.Scale.X, copy.Scale.Y));
            PoseLayer(ent, copyKey, ProtoMan.Index(fallbackMap).Displacement, layers, temporary: true);
            if (_sprite.TryGetLayer(ent.AsNullable(), $"cmu-surrender-{copyKey}-displacement", out var dataLayer, false))
                _sprite.LayerSetDirOffset(dataLayer, SpriteComponent.DirectionOffset.Flip);
        }

        var alias = $"cmu-surrender-{key}";
        var shader = layer.Shader;
        var prototype = layer.ShaderPrototype;

        _sprite.LayerMapSet(ent.AsNullable(), alias, index);
        if (!_displacement.TryAddDisplacement(map, ent, index, alias, out var displacementKey))
        {
            _sprite.LayerMapRemove(ent.AsNullable(), alias);
            if (temporary)
            {
                if (key is string temporaryKey)
                    _sprite.RemoveLayer(ent.AsNullable(), temporaryKey, false);
                if (shader?.Mutable == true)
                    shader.Dispose();
            }
            return;
        }

        var poseShader = layer.Shader?.Duplicate();
        ent.Comp.LayerSetShader(alias, poseShader, layer.ShaderPrototype?.Id);
        var displacementLayer = (SpriteComponent.Layer) ent.Comp[displacementKey];
        layers.Add(new PosedLayer(alias, layer, displacementKey, displacementLayer, shader, prototype, poseShader, temporary));
    }

    private void UpdateFacing(EntityUid uid, SpriteComponent sprite, CMUSurrenderFacingComponent facing)
    {
        var requested = facing.OriginalOverride
            ? facing.OriginalDirection
            : (_transform.GetWorldRotation(uid) + _eye.CurrentEye.Rotation).GetCardinalDir();
        if (requested is Direction.North or Direction.South)
            facing.VerticalDirection = requested;
        sprite.DirectionOverride = facing.VerticalDirection;
        sprite.EnableDirectionOverride = true;
    }

    private void RestoreFacing(EntityUid uid)
    {
        if (!TryComp<CMUSurrenderFacingComponent>(uid, out var facing))
            return;
        if (TryComp<SpriteComponent>(uid, out var sprite))
        {
            sprite.DirectionOverride = facing.OriginalDirection;
            sprite.EnableDirectionOverride = facing.OriginalOverride;
        }
        RemComp<CMUSurrenderFacingComponent>(uid);
    }

    private void Restore(EntityUid uid)
    {
        if (!_posed.Remove(uid, out var layers))
            return;

        if (!TryComp<SpriteComponent>(uid, out var sprite))
        {
            foreach (var entry in layers)
            {
                if (entry.Temporary && entry.OriginalShader?.Mutable == true)
                    entry.OriginalShader.Dispose();
                entry.PoseShader?.Dispose();
            }
            return;
        }

        // Resolve ownership once, rather than scanning the sprite for every removed layer.
        // Object identity also survives a visual refresh removing or reusing our map keys.
        var indices = new Dictionary<ISpriteLayer, int>();
        var index = 0;
        foreach (var layer in sprite.AllLayers)
            indices.Add(layer, index++);

        var removals = new List<int>(layers.Count * 2);
        foreach (var entry in layers)
        {
            if (indices.TryGetValue(entry.DisplacementLayer, out var displacementIndex))
                removals.Add(displacementIndex);

            if (!indices.TryGetValue(entry.Layer, out var layerIndex))
                continue;

            if (entry.Temporary)
                removals.Add(layerIndex);
            else if (entry.Layer.Shader == entry.PoseShader)
            {
                if (entry.OriginalShader == null && entry.OriginalPrototype is { } prototype)
                    sprite.LayerSetShader(layerIndex, prototype.Id);
                else
                    sprite.LayerSetShader(layerIndex, entry.OriginalShader, entry.OriginalPrototype?.Id);
            }
        }

        // Descending removal keeps the saved indices valid as the sprite list shrinks.
        removals.Sort();
        for (var i = removals.Count - 1; i >= 0; i--)
        {
            if (i + 1 < removals.Count && removals[i] == removals[i + 1])
                continue;
            _sprite.RemoveLayer((uid, sprite), removals[i], false);
        }

        foreach (var entry in layers)
        {
            if (!entry.Temporary)
                _sprite.LayerMapRemove((uid, sprite), entry.Alias);
            else if (entry.OriginalShader?.Mutable == true)
                entry.OriginalShader.Dispose();
            entry.PoseShader?.Dispose();
        }
    }

    private static bool HasLimb(HumanoidVisualLayers layer, int arms, int hands)
    {
        return layer switch
        {
            HumanoidVisualLayers.LArm => (arms & 1) != 0,
            HumanoidVisualLayers.RArm => (arms & 2) != 0,
            HumanoidVisualLayers.LHand => (hands & 1) != 0,
            HumanoidVisualLayers.RHand => (hands & 2) != 0,
            _ => true,
        };
    }

    private static bool IsArmLayer(HumanoidVisualLayers layer)
    {
        return layer is HumanoidVisualLayers.LArm or HumanoidVisualLayers.RArm
            or HumanoidVisualLayers.LHand or HumanoidVisualLayers.RHand;
    }
}
