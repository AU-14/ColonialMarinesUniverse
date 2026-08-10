using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Prototypes;
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
    private static readonly SatiationValue Parched = "Parched";
    private static readonly SatiationValue Starving = "Starving";

    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    private readonly SatiationSystem _satiation;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;
    private readonly ShaderInstance _shader;

    public NutritionOverlay()
    {
        IoCManager.InjectDependencies(this);
        _satiation = _entityManager.System<SatiationSystem>();
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

        if (!_entityManager.TryGetComponent(player, out SatiationComponent? satiation))
            return false;

        var severelyDehydrated = _satiation.IsValueInRange(
            (player.Value, satiation),
            SatiationSystem.Thirst,
            below: Parched);
        var severelyStarved = _satiation.IsValueInRange(
            (player.Value, satiation),
            SatiationSystem.Hunger,
            below: Starving);

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
