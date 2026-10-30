using Content.Shared.CMU14.Yautja;
using Content.Shared.Hands;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client.CMU14.Yautja;

public sealed partial class CMUYautjaVisualsSystem
{
    private readonly List<(EntityUid Owner, string Key, ISpriteLayer Layer)> _hiddenHands = new();
    private readonly List<EntityUid> _hiddenItems = new();
    private readonly Dictionary<EntityUid, (EntityUid User, string[] Layers)> _heldVisuals = new();

    private void InitializeItems()
    {
        SubscribeLocalEvent<YautjaSmartDiscComponent, HeldVisualsUpdatedEvent>(OnDiscHeld);
        SubscribeLocalEvent<YautjaChainedWeaponComponent, HeldVisualsUpdatedEvent>(OnCombiHeld);
        SubscribeLocalEvent<YautjaCasterComponent, HeldVisualsUpdatedEvent>(OnCasterHeld);
        SubscribeLocalEvent<YautjaFalconDroneComponent, HeldVisualsUpdatedEvent>(OnFalconHeld);
    }

    private void OnDiscHeld(Entity<YautjaSmartDiscComponent> ent, ref HeldVisualsUpdatedEvent args) => TrackHeld(ent, args);
    private void OnCombiHeld(Entity<YautjaChainedWeaponComponent> ent, ref HeldVisualsUpdatedEvent args) => TrackHeld(ent, args);
    private void OnCasterHeld(Entity<YautjaCasterComponent> ent, ref HeldVisualsUpdatedEvent args) => TrackHeld(ent, args);
    private void OnFalconHeld(Entity<YautjaFalconDroneComponent> ent, ref HeldVisualsUpdatedEvent args) => TrackHeld(ent, args);

    private void TrackHeld(EntityUid item, HeldVisualsUpdatedEvent args)
    {
        if (args.RevealedLayers.Count == 0)
            _heldVisuals.Remove(item);
        else
            _heldVisuals[item] = (args.User, [.. args.RevealedLayers]);
    }

    private void RestoreItems()
    {
        foreach (var (owner, key, layer) in _hiddenHands)
        {
            if (TryComp<SpriteComponent>(owner, out var sprite) &&
                _sprites.LayerMapTryGet((owner, sprite), key, out var index, true) &&
                ReferenceEquals(sprite[index], layer))
                _sprites.LayerSetVisible((owner, sprite), index, true);
        }
        foreach (var uid in _hiddenItems)
        {
            if (TryComp<SpriteComponent>(uid, out var sprite))
                _sprites.SetVisible((uid, sprite), true);
        }
        _hiddenHands.Clear();
        _hiddenItems.Clear();
    }

    private void UpdateReturningItems()
    {
        RestoreItems();
        var effects = EntityQueryEnumerator<CMUYautjaEffectComponent>();
        while (effects.MoveNext(out _, out var effect))
        {
            if (_timing.CurTime >= effect.EndsAt || effect.Item is not { } item || effect.Target is not { } user)
                continue;
            if (TryComp<SpriteComponent>(item, out var itemSprite) && itemSprite.Visible)
            {
                _hiddenItems.Add(item);
                _sprites.SetVisible((item, itemSprite), false);
            }
            if (!_heldVisuals.TryGetValue(item, out var held) || held.User != user ||
                !TryComp<SpriteComponent>(user, out var sprite))
                continue;
            foreach (var key in held.Layers)
            {
                if (!_sprites.LayerMapTryGet((user, sprite), key, out var index, true) || !sprite[index].Visible)
                    continue;
                _hiddenHands.Add((user, key, sprite[index]));
                _sprites.LayerSetVisible((user, sprite), index, false);
            }
        }
    }
}
