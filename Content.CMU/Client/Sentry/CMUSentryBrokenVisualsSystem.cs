using Content.Shared.CMU14.Sentry;
using Robust.Client.GameObjects;

namespace Content.Client.CMU14.Sentry;

/// <summary>
///     A broken sentry is drawn charred black until it's repaired.
/// </summary>
public sealed class CMUSentryBrokenVisualsSystem : EntitySystem
{
    private static readonly Color Charred = new(0.12f, 0.12f, 0.12f);

    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUSentryBrokenComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CMUSentryBrokenComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<CMUSentryBrokenComponent> ent, ref ComponentStartup args)
    {
        if (TryComp(ent, out SpriteComponent? sprite))
            _sprite.SetColor((ent, sprite), Charred);
    }

    private void OnShutdown(Entity<CMUSentryBrokenComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp(ent, out SpriteComponent? sprite))
            _sprite.SetColor((ent, sprite), Color.White);
    }
}
