using Content.Shared._CMU14.Xenomorphs.Larva;
using Robust.Client.GameObjects;
using Robust.Shared.Maths;

namespace Content.Client._CMU14.Xenomorphs.Larva;

public sealed class BloodyLarvaVisualsSystem : EntitySystem
{
    private const float FadeDuration = 1.0f; // seconds
    private const string BloodyLayer = "bloody";

    private readonly Dictionary<EntityUid, float> _fading = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<AppearanceComponent, AppearanceChangeEvent>(OnAppearance);
    }

    private void OnAppearance(Entity<AppearanceComponent> ent, ref AppearanceChangeEvent args)
    {
        if (!args.AppearanceData.TryGetValue(BloodyLarvaVisuals.Bloody, out var value) || value is not bool bloody)
            return;

        if (!TryComp<SpriteComponent>(ent, out var sprite) || !sprite.LayerMapTryGet(BloodyLayer, out var layer))
            return;

        if (bloody)
        {
            _fading.Remove(ent.Owner);
            sprite.LayerSetVisible(layer, true);
            sprite.LayerSetColor(layer, Color.White);
        }
        else
        {
            _fading[ent.Owner] = 0f;
        }
    }

    public override void Update(float frameTime)
    {
        if (_fading.Count == 0)
            return;

        var finished = new List<EntityUid>();

        foreach (var (uid, progress) in _fading)
        {
            if (!TryComp<SpriteComponent>(uid, out var sprite) || !sprite.LayerMapTryGet(BloodyLayer, out var layer))
            {
                finished.Add(uid);
                continue;
            }

            var newProgress = progress + frameTime / FadeDuration;

            if (newProgress >= 1f)
            {
                sprite.LayerSetVisible(layer, false);
                sprite.LayerSetColor(layer, Color.White);
                finished.Add(uid);
                continue;
            }

            sprite.LayerSetColor(layer, Color.White.WithAlpha(1f - newProgress));
            _fading[uid] = newProgress;
        }

        foreach (var uid in finished)
        {
            _fading.Remove(uid);
        }
    }
}