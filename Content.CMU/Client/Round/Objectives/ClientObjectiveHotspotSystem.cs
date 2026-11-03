using Content.Shared.CMU14.Round.Objectives.Type;
using Robust.Client.GameObjects;

namespace Content.Client.CMU14.Round.Objectives;

public sealed partial class ClientObjectiveHotspotSystem : EntitySystem
{
    // Matches HotspotObjectiveComponent.ZoneColorHex; appearance data overrides it.
    private static readonly Color NeutralColor = Color.FromHex("#FFB300");

    [Dependency] private readonly AppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HotspotObjectiveComponent, ComponentStartup>(OnHotspotStartup);
        SubscribeLocalEvent<HotspotObjectiveComponent, AppearanceChangeEvent>(OnHotspotAppearance);
    }

    private void OnHotspotStartup(EntityUid uid, HotspotObjectiveComponent comp, ref ComponentStartup args)
    {
        if (TryComp<SpriteComponent>(uid, out var sprite))
            sprite.Color = NeutralColor;
    }

    private void OnHotspotAppearance(EntityUid uid, HotspotObjectiveComponent comp, ref AppearanceChangeEvent args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        if (_appearance.TryGetData(uid, HotspotVisuals.ZoneColor, out string? hex, args.Component)
            && Color.TryFromHex(hex, out var color))
            sprite.Color = color;
        else
            sprite.Color = NeutralColor;
    }
}
