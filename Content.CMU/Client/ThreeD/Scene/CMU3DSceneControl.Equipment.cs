using System.Numerics;
using Content.Shared.CMU14.ThreeD;
using Content.Client.Inventory;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Standing;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Graphics;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client.CMU14.ThreeD.Scene;

public sealed partial class CMU3DSceneControl
{
    private const int EquipmentLimit = 64;
    private const int EquipmentTexels = 12;
    private const int EquipmentAssemblyParts = 512;
    private const int EquipmentParts = EquipmentLimit * EquipmentAssemblyParts;
    private const int EquipmentRows = EquipmentParts * CMU3DSceneEncoding.BoxTexels / 256;
    [Dependency] private IPlayerManager _equipmentPlayers = default!;
    private readonly Dictionary<(string Prototype, string Slot), List<CMU3DEquipmentPosePrototype>> _equipmentPoses = [];
    private readonly List<EquipmentGroup> _equipment = [];
    private readonly List<EquipmentGroup> _nextEquipment = [];
    private readonly List<EntityUid> _equipmentWearers = [];
    private readonly List<CMU3DSceneBox> _equipmentBoxes = [];
    private readonly Rgba32[] _equipmentPixels = new Rgba32[EquipmentParts * CMU3DSceneEncoding.BoxTexels];
    private readonly Rgba32[] _equipmentRootPixels = new Rgba32[EquipmentLimit * EquipmentTexels];
    private OwnedTexture? _equipmentTexture;
    private OwnedTexture? _equipmentRoots;
    private bool _equipmentLoaded;

    private sealed record EquipmentGroup(EntityUid Wearer, EntityUid Item, CMU3DEquipmentPosePrototype Pose,
        IReadOnlyList<CMU3DModelPart> Parts, Color Tint, string Appearance, bool Left, bool Own)
    {
        public int Start;
        public int Count;
        public Vector3 Low;
        public Vector3 High;
        public CMU3DEquipmentTransform Transform;
    }

    private void PrepareEquipment()
    {
        if (!_equipmentLoaded)
        {
            foreach (var pose in _prototypes.EnumeratePrototypes<CMU3DEquipmentPosePrototype>())
            {
                if (pose.Scale <= 0 || !float.IsFinite(pose.Scale) || pose.Scale > 8)
                    continue;
                foreach (var prototype in pose.SourcePrototypes)
                {
                    var key = (prototype, pose.Slot);
                    if (!_equipmentPoses.TryGetValue(key, out var variants))
                        _equipmentPoses[key] = variants = [];
                    variants.Add(pose);
                }
            }
            _equipmentLoaded = true;
        }
        var transforms = _entities.System<SharedTransformSystem>();
        var inventory = _entities.System<InventorySystem>();
        var hands = _entities.System<SharedHandsSystem>();
        var source = _entities.System<CMU3DLiveSceneSystem>();
        var elevation = _entities.System<CMU3DElevationSystem>();
        var own = _equipmentPlayers.LocalEntity;
        _equipmentWearers.Clear();
        if (own is { } controlled) _equipmentWearers.Add(controlled);
        for (var i = 0; i < Math.Min(BillboardLimit, _billboardCandidates.Count); i++)
            if (_billboardCandidates[i].Uid != own) _equipmentWearers.Add(_billboardCandidates[i].Uid);
        _nextEquipment.Clear();
        foreach (var wearer in _equipmentWearers)
        {
            if (!_entities.TryGetComponent(wearer, out TransformComponent? xform) ||
                !SceneMaps.Contains(xform.MapID) ||
                !_entities.TryGetComponent(wearer, out SpriteComponent? wearerSprite) || !wearerSprite.Visible || wearerSprite.Scale != Vector2.One)
                continue;
            if (_entities.TryGetComponent(wearer, out StandingStateComponent? standing) && !standing.Standing)
                continue;
            if (_entities.TryGetComponent(wearer, out InventoryComponent? slots))
            {
                var enumerator = inventory.GetSlotEnumerator((wearer, slots));
                while (enumerator.NextItem(out var item, out var slot))
                    Add(item, slot.Name, false);
            }
            if (_entities.TryGetComponent(wearer, out HandsComponent? holder) && holder.ShowInHands)
            {
                foreach (var (name, hand) in holder.Hands)
                    if (hands.TryGetHeldItem((wearer, holder), name, out var item, hideVirtualItems: true))
                        Add(item.Value, "hand", hand.Location == HandLocation.Left);
            }

            void Add(EntityUid item, string slot, bool left)
            {
                if (_nextEquipment.Count >= EquipmentLimit ||
                    wearer == own && slot is "head" or "eyes" or "mask" or "ears" ||
                    !_entities.TryGetComponent(item, out MetaDataComponent? meta) || meta.EntityPrototype == null ||
                    !_equipmentPoses.TryGetValue((meta.EntityPrototype.ID, slot), out var variants))
                    return;
                foreach (var pose in variants)
                {
                    if (!_prototypes.TryIndex(pose.Model, out var model)) continue;
                    IReadOnlyList<CMU3DModelPart> parts;
                    Color tint;
                    var appearance = string.Empty;
                    if (pose.WornAppearance)
                    {
                        if (!source.TryWornEquipmentAppearance(wearer, wearerSprite, slot, pose, out tint)) continue;
                        parts = model.Parts;
                    }
                    else if (!source.TryEquipmentParts(item, model, pose, out parts, out tint, out appearance))
                        continue;
                    if (parts.Count is 0 or > EquipmentAssemblyParts) return;
                    var position = new Vector3(transforms.GetWorldPosition(xform) - SceneOrigin,
                        elevation.PhysicalHeight(wearer, SceneDepth));
                    var next = _nextEquipment.Count;
                    EquipmentGroup group;
                    if (next < _equipment.Count && _equipment[next] is var previous &&
                        previous.Wearer == wearer && previous.Item == item && previous.Pose == pose &&
                        previous.Parts == parts && previous.Tint == tint && previous.Appearance == appearance &&
                        previous.Left == left && previous.Own == (wearer == own))
                        group = previous;
                    else
                        group = new EquipmentGroup(wearer, item, pose, parts, tint, appearance, left, wearer == own);
                    var transform = CMU3DEquipmentTransform.Create(pose, position,
                        (float) transforms.GetWorldRotation(xform).Theta,
                        wearer == own && slot == "hand" ? Camera() : null, left);
                    // Root positions use signed 16-bit fixed point. Clamping a remote
                    // wearer would drag its equipment toward the scene boundary.
                    if (Math.Max(Math.Abs(transform.Origin.X), Math.Max(Math.Abs(transform.Origin.Y), Math.Abs(transform.Origin.Z))) > 31)
                        return;
                    group.Transform = transform.Quantized();
                    _nextEquipment.Add(group);
                    break;
                }
            }
        }
        var changed = _equipment.Count != _nextEquipment.Count;
        for (var i = 0; !changed && i < _equipment.Count; i++)
        {
            var before = _equipment[i];
            var after = _nextEquipment[i];
            changed = before.Item != after.Item || before.Pose != after.Pose || before.Parts != after.Parts ||
                      before.Tint != after.Tint || before.Appearance != after.Appearance ||
                      before.Wearer != after.Wearer || before.Own != after.Own || before.Left != after.Left;
        }
        if (changed)
        {
            _redraw = true;
            _equipment.Clear();
            _equipment.AddRange(_nextEquipment);
            _equipmentBoxes.Clear();
            foreach (var group in _equipment)
            {
                group.Start = _equipmentBoxes.Count;
                group.Low = new Vector3(float.PositiveInfinity);
                group.High = new Vector3(float.NegativeInfinity);
                foreach (var part in group.Parts)
                {
                    if (!part.Valid) continue;
                    var surface = part.Surface is { } id ? _prototypes.Index(id).AtlasIndex : (ushort) 0;
                    var box = new CMU3DSceneBox((part.Min + part.Max) / 2, (part.Max - part.Min) / 2,
                        part.YawRadians, part.Color * group.Tint, group.Wearer, part.Shape,
                        surface, part.SurfaceAxis, 1, part.SurfaceFlipU) { Pitch = part.PitchRadians };
                    var index = _equipmentBoxes.Count;
                    CMU3DSceneEncoding.WriteBox(_equipmentPixels, index, box);
                    box = CMU3DSceneEncoding.Quantize(box);
                    _equipmentBoxes.Add(box);
                    group.Low = Vector3.Min(group.Low, box.Center - box.AxisAlignedHalfSize);
                    group.High = Vector3.Max(group.High, box.Center + box.AxisAlignedHalfSize);
                }
                group.Count = _equipmentBoxes.Count - group.Start;
            }
        }
        var parameters = TextureLoadParameters.Default;
        parameters.Srgb = false;
        parameters.SampleParameters = new TextureSampleParameters { Filter = false };
        _equipmentTexture ??= _clyde.CreateBlankTexture<Rgba32>(new Vector2i(256, EquipmentRows), "cmu-3d-equipment", parameters);
        _equipmentRoots ??= _clyde.CreateBlankTexture<Rgba32>(new Vector2i(EquipmentLimit * EquipmentTexels, 1), "cmu-3d-equipment-poses", parameters);
        if (changed && _equipmentBoxes.Count > 0)
        {
            var rows = (_equipmentBoxes.Count * CMU3DSceneEncoding.BoxTexels + 255) / 256;
            _equipmentTexture.SetSubImage(Vector2i.Zero, new Vector2i(256, rows), _equipmentPixels.AsSpan(0, rows * 256));
        }
        for (var i = 0; i < _equipment.Count; i++)
        {
            var group = _equipment[i];
            group.Transform = _nextEquipment[i].Transform;
            var t = group.Transform;
            var offset = i * EquipmentTexels;
            _equipmentRootPixels[offset] = PackPair(t.Origin.X, t.Origin.Y);
            _equipmentRootPixels[offset + 1] = PackPair(t.Origin.Z, t.X.X);
            _equipmentRootPixels[offset + 2] = PackPair(t.X.Y, t.X.Z);
            _equipmentRootPixels[offset + 3] = PackPair(t.Y.X, t.Y.Y);
            _equipmentRootPixels[offset + 4] = PackPair(t.Y.Z, t.Z.X);
            _equipmentRootPixels[offset + 5] = PackPair(t.Z.Y, t.Z.Z);
            _equipmentRootPixels[offset + 6] = PackPair(group.Low.X, group.Low.Y);
            _equipmentRootPixels[offset + 7] = PackPair(group.Low.Z, group.High.X);
            _equipmentRootPixels[offset + 8] = PackPair(group.High.Y, group.High.Z);
            _equipmentRootPixels[offset + 9] = new Rgba32((byte) group.Start, (byte) (group.Start >> 8), (byte) group.Count, (byte) (group.Count >> 8));
            var center = t.Point((group.Low + group.High) / 2);
            var radius = (group.High - group.Low).Length() * group.Pose.Scale / 2 + .01f;
            _equipmentRootPixels[offset + 10] = PackPair(center.X, center.Y);
            _equipmentRootPixels[offset + 11] = PackPair(center.Z, radius);
        }
        if (_equipment.Count > 0)
            _equipmentRoots.SetSubImage(Vector2i.Zero, new Vector2i(_equipment.Count * EquipmentTexels, 1),
                _equipmentRootPixels.AsSpan(0, _equipment.Count * EquipmentTexels));
    }

    private List<int> HideEquipmentLayers(EntityUid wearer, SpriteComponent sprite, SpriteSystem sprites)
    {
        var hidden = new List<int>();
        foreach (var group in _equipment)
        {
            if (group.Wearer != wearer) continue;
            HashSet<string>? keys = null;
            if (group.Pose.Slot == "hand" && _entities.TryGetComponent(wearer, out HandsComponent? hands))
            {
                IReadOnlyDictionary<HandLocation, HashSet<string>> revealed = hands.RevealedLayers;
                revealed.TryGetValue(group.Left ? HandLocation.Left : HandLocation.Right, out keys);
            }
            else if (_entities.TryGetComponent(wearer, out InventorySlotsComponent? inventory))
                inventory.VisualLayerKeys.TryGetValue(group.Pose.Slot, out keys);
            if (keys == null) continue;
            foreach (var key in keys)
                if (sprites.LayerMapTryGet((wearer, sprite), key, out var index, false) && sprite[index].Visible)
                {
                    hidden.Add(index);
                    sprites.LayerSetVisible((wearer, sprite), index, false);
                }
        }
        return hidden;
    }

    private void PickEquipment(Vector3 origin, Vector3 ray, ref float distance, ref EntityUid? target)
    {
        foreach (var group in _equipment)
        {
            if (group.Own) continue;
            var localOrigin = group.Transform.InversePoint(origin);
            var localRay = group.Transform.InverseDirection(ray);
            for (var i = group.Start; i < group.Start + group.Count; i++)
                if (CMU3DSceneEncoding.Intersect(_equipmentBoxes[i], localOrigin, localRay, out var hit, _surfaces) && hit < distance)
                {
                    distance = hit;
                    target = group.Wearer;
                }
        }
    }

    private void ReleaseEquipment()
    {
        _equipmentTexture?.Dispose();
        _equipmentRoots?.Dispose();
        _equipmentTexture = null;
        _equipmentRoots = null;
        _equipment.Clear();
        _nextEquipment.Clear();
        _equipmentBoxes.Clear();
        _equipmentWearers.Clear();
        _equipmentPoses.Clear();
        _equipmentLoaded = false;
    }
}
