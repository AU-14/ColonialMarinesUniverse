using Content.Shared.CMU14.Yautja.BadBlood;
using Robust.Client.GameObjects;

namespace Content.Client.CMU14.Yautja.BadBlood;

/// <summary>
/// CMU14 ECS-703 (D16): tints a raging Berserker's sprite red while <see cref="BadBloodBloodRageComponent"/> is on
/// them, and puts the colour back when it goes. The rage drops the cloak, so the cloak never fights the tint.
/// </summary>
public sealed class BadBloodBloodRageVisualizerSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    private readonly Dictionary<EntityUid, Color> _before = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<BadBloodBloodRageComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BadBloodBloodRageComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<BadBloodBloodRageComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp(ent, out SpriteComponent? sprite))
            return;

        _before[ent.Owner] = sprite.Color;
        _sprite.SetColor((ent.Owner, sprite), ent.Comp.Tint);
    }

    private void OnShutdown(Entity<BadBloodBloodRageComponent> ent, ref ComponentShutdown args)
    {
        if (!_before.Remove(ent.Owner, out var color) || !TryComp(ent, out SpriteComponent? sprite))
            return;

        _sprite.SetColor((ent.Owner, sprite), color);
    }
}
