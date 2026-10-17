using Content.Client.Nutrition.EntitySystems;
using Robust.Client.Graphics;

namespace Content.Client.Nutrition;

public sealed partial class NutritionOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayMan = default!;
    [Dependency] private ClientSatiationSystem _satiation = default!;

    private NutritionOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new(_satiation);
        _overlayMan.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlayMan.RemoveOverlay(_overlay);
    }
}
