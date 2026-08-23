using Content.Client.DamageState;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Rest;
using Content.Shared.Mobs;
using Robust.Client.GameObjects;

namespace Content.Client._CMU14.Yautja;

public sealed partial class YautjaHellhoundVisualsSystem : EntitySystem
{
    private const string Walking = "Normal Hellhound Walking";
    private const string Sleeping = "Normal Hellhound Sleeping";
    private const string KnockedDown = "Normal Hellhound Knocked Down";
    private const string Dead = "Normal Hellhound Dead";

    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaHellhoundComponent, AppearanceChangeEvent>(
            OnAppearanceChange,
            after: [typeof(DamageStateVisualizerSystem)]);
    }

    private void OnAppearanceChange(Entity<YautjaHellhoundComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null ||
            !_sprite.LayerMapTryGet((ent.Owner, args.Sprite), DamageStateVisualLayers.Base, out var layer, false))
        {
            return;
        }

        if (!_appearance.TryGetData(ent, MobStateVisuals.State, out MobState state, args.Component))
            state = MobState.Alive;

        var spriteState = state switch
        {
            MobState.Dead => Dead,
            MobState.Critical => KnockedDown,
            _ when _appearance.TryGetData(ent, XenoVisualLayers.Base, out XenoRestState rest, args.Component) &&
                   rest == XenoRestState.Resting => Sleeping,
            _ => Walking,
        };

        _sprite.LayerSetRsiState((ent.Owner, args.Sprite), layer, spriteState);
    }
}
