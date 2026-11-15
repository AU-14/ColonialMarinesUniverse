// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Clothing;
using Content.Shared.CMU14.Medical.Autocompressor;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;

namespace Content.Client.CMU14.Medical.Autocompressor;

public sealed partial class CMUAutocompressorVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    private readonly List<(string Key, int Index)> _layers = new();

    public override void Initialize()
    {
        base.Initialize();
        InitializeTraining();
        SubscribeLocalEvent<CMUAutocompressorComponent, EquipmentVisualsUpdatedEvent>(OnEquipmentVisualsUpdated);
    }

    private void OnEquipmentVisualsUpdated(Entity<CMUAutocompressorComponent> ent, ref EquipmentVisualsUpdatedEvent args)
    {
        if (args.Slot != CMUAutocompressorSystem.PatientSlot || !TryComp<SpriteComponent>(args.Equipee, out var sprite))
            return;

        Entity<SpriteComponent?> body = (args.Equipee, sprite);
        if (!_sprite.LayerMapTryGet(body, HumanoidVisualLayers.Handcuffs, out var anchor, false))
            return;

        _layers.Clear();
        foreach (var key in args.RevealedLayers)
        {
            if (_sprite.LayerMapTryGet(body, key, out var index, false) && index < anchor)
                _layers.Add((key, index));
        }
        _layers.Sort(static (left, right) => left.Index.CompareTo(right.Index));

        string? previousKey = null;
        foreach (var (key, _) in _layers)
        {
            if (!_sprite.LayerMapTryGet(body, key, out var index, false)
                || !_sprite.RemoveLayer(body, index, out var layer, false))
                continue;
            if (previousKey == null)
                _sprite.LayerMapTryGet(body, HumanoidVisualLayers.Handcuffs, out anchor, false);
            else
                _sprite.LayerMapTryGet(body, previousKey, out anchor, false);
            var inserted = _sprite.AddLayer(body, layer, anchor + 1);
            _sprite.LayerMapSet(body, key, inserted);
            previousKey = key;
        }
        _layers.Clear();
    }
}
