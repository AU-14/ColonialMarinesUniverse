using Content.Client.Nutrition.EntitySystems;
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
    private static readonly Dictionary<SatiationValue, bool> SevereHungerThreshold = new()
    {
        ["Starving"] = true,
    };
    private static readonly Dictionary<SatiationValue, bool> SevereThirstThreshold = new()
    {
        ["Parched"] = true,
    };

    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    private readonly ClientSatiationSystem _satiation;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;
    private readonly ShaderInstance _shader;

    public NutritionOverlay(ClientSatiationSystem satiation)
    {
        _satiation = satiation;
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

        if (!_entityManager.TryGetComponent(player, out SatiationComponent? satiation))
            return false;

        var entity = new Entity<SatiationComponent>(player.Value, satiation);
        var severelyDehydrated = _satiation.TryGetValueByThreshold(
            entity,
            SatiationSystem.Thirst,
            SevereThirstThreshold,
            out _,
            out _,
            out _);
        var severelyStarved = _satiation.TryGetValueByThreshold(
            entity,
            SatiationSystem.Hunger,
            SevereHungerThreshold,
            out _,
            out _,
            out _);

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
