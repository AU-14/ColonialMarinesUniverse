using Content.Shared._CMU14.Yautja;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._CMU14.Yautja;

public sealed class YautjaHealingGunVisualizerSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedItemSystem _item = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<YautjaHealingGunComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<YautjaHealingGunComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);
    }

    private void OnStartup(Entity<YautjaHealingGunComponent> ent, ref ComponentStartup args)
    {
        UpdateVisuals(ent);
    }

    private void OnAfterAutoHandleState(Entity<YautjaHealingGunComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateVisuals(ent);
    }

    private void UpdateVisuals(Entity<YautjaHealingGunComponent> ent)
    {
        if (!TryComp(ent, out SpriteComponent? sprite))
            return;

        var state = ent.Comp.Loaded ? "healing_gun" : "healing_gun_empty";
        _sprite.LayerSetSprite(
            (ent.Owner, sprite),
            0,
            new SpriteSpecifier.Rsi(new ResPath("_CMU14/Yautja/medical.rsi"), state));
        _item.VisualsChanged(ent.Owner);
    }
}
