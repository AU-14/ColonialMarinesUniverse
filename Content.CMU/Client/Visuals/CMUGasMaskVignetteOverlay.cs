using System.Numerics;
using Content.Shared.Clothing.Components;
using Content.Shared.Inventory;
using Content.Shared.Tag;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Visuals;

public sealed class CMUGasMaskVignetteOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> CircleMaskShader = "GradientCircleMask";
    private static readonly ProtoId<TagPrototype> GasMaskTag = "GasMask";

    private const float OuterAlpha = 0.288f;
    private const float FadeStart = 0.38f;
    private const float FadeEnd = 0.56f;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly IEntityManager _entities;
    private readonly IPlayerManager _player;
    private readonly InventorySystem _inventory;
    private readonly TagSystem _tag;
    private readonly ShaderInstance _shader;

    public CMUGasMaskVignetteOverlay(IEntityManager entManager, IPlayerManager player, IPrototypeManager prototypes)
    {
        _entities = entManager;
        _player = player;
        _inventory = entManager.System<InventorySystem>();
        _tag = entManager.System<TagSystem>();
        _shader = prototypes.Index(CircleMaskShader).InstanceUnique();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _player.LocalEntity is { } local &&
               _inventory.TryGetSlotEntity(local, "mask", out var mask) &&
               _tag.HasTag(mask.Value, GasMaskTag) &&
               !(_entities.TryGetComponent(mask.Value, out MaskComponent? maskComp) && maskComp.IsToggled);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var radius = Math.Min(args.ViewportBounds.Width, args.ViewportBounds.Height);

        _shader.SetParameter("color", new Vector3(0f, 0f, 0f));
        _shader.SetParameter("darknessAlphaOuter", OuterAlpha);
        _shader.SetParameter("innerCircleRadius", FadeStart * radius);
        _shader.SetParameter("innerCircleMaxRadius", FadeStart * radius);
        _shader.SetParameter("outerCircleRadius", FadeEnd * radius);
        _shader.SetParameter("outerCircleMaxRadius", FadeEnd * radius);

        var handle = args.WorldHandle;
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldAABB, Color.White);
        handle.UseShader(null);
    }
}
