using Content.Shared._RMC14.Xenonids.Screech;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._RMC14.Xenonids.Screech;

public sealed partial class ScreechBlindOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> ScreechBlindShader = "RMCScreechBlind";

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IEntityManager _entityManager = default!;

    private readonly ShaderInstance _shader;

    public ScreechBlindOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _prototypeManager.Index(ScreechBlindShader).InstanceUnique();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!_entityManager.TryGetComponent(_playerManager.LocalEntity, out ScreechBlindComponent? blind))
            return;

        if (ScreenTexture == null || args.Viewport.Eye == null)
            return;

        var viewportSize = args.Viewport.Size;
        var worldHeight = args.WorldBounds.Box.Height;
        var pixelsPerTile = viewportSize.Y / worldHeight;

        var innerRadius = blind.Radius * pixelsPerTile;
        var outerRadius = (blind.Radius + 1f) * pixelsPerTile;

        var handle = args.WorldHandle;
        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("innerRadius", (float)innerRadius);
        _shader.SetParameter("outerRadius", (float)outerRadius);

        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
