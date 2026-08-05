using Content.Shared.Nutrition.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client.Nutrition;

/// <summary>
/// Desaturates the screen while the local player is severely overdue for a drink or a meal.
/// </summary>
public sealed partial class NutritionOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Shader = "GreyscaleFullscreen";

    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;
    private readonly ShaderInstance _shader;

    public NutritionOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _prototypeManager.Index(Shader).InstanceUnique();
        ZIndex = 11;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        var player = _playerManager.LocalEntity;
        if (player == null)
            return false;

        if (!_entityManager.TryGetComponent(player, out EyeComponent? eyeComp) || args.Viewport.Eye != eyeComp.Eye)
            return false;

        var severelyDehydrated = _entityManager.TryGetComponent(player, out ThirstComponent? thirst) &&
            thirst.CurrentThirstThreshold <= ThirstThreshold.Parched;
        var severelyStarved = _entityManager.TryGetComponent(player, out HungerComponent? hunger) &&
            hunger.CurrentThreshold <= HungerThreshold.Starving;

        return severelyDehydrated || severelyStarved;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var handle = args.WorldHandle;
        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
