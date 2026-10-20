using Content.Shared.CMU14.Round.Objectives.Type;
using Robust.Client.GameObjects;

namespace Content.Client.CMU14.Round.Objectives;

public sealed partial class ClientObjectiveHotspotSystem : EntitySystem
{
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
        if (args.Component.TryGetData(HotspotVisuals.Controller, out string? controller))
            UpdateZoneColor(uid, controller);
    }

    private static void UpdateZoneColor(EntityUid uid, string? controller)
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
