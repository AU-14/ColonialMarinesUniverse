using Content.Shared.CMU14.Round.Objectives.Type;
using Robust.Client.GameObjects;

namespace Content.Client.CMU14.Round.Objectives;

public sealed partial class ClientObjectiveHotspotSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HotspotObjectiveComponent, ComponentStartup>(OnHotspotStartup);
        SubscribeLocalEvent<HotspotObjectiveComponent, AppearanceChangeEvent>(OnHotspotAppearance);
    }

    private void OnHotspotStartup(EntityUid uid, HotspotObjectiveComponent comp, ref ComponentStartup args)
        => UpdateZoneColor(uid, comp.CurrentController);

    private void OnHotspotAppearance(EntityUid uid, HotspotObjectiveComponent comp, ref AppearanceChangeEvent args)
    {
        if (_appearance.TryGetData(uid, HotspotVisuals.Controller, out string? controller, args.Component))
            UpdateZoneColor(uid, controller);
    }

    private void UpdateZoneColor(EntityUid uid, string? controller)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        sprite.Color = controller switch
        {
            "govfor" => Color.FromHex("#4CA44C"),
            "opfor" => Color.FromHex("#FF4136"),
            _ => Color.FromHex("#FFB300"),
        };
    }
}
