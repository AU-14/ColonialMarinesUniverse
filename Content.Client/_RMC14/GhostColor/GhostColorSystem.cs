using Content.Shared._RMC14.GhostColor;
using Robust.Client.GameObjects;

namespace Content.Client._RMC14.GhostColor;

public sealed partial class GhostColorSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Update(float frameTime)
    {
        var defaultColor = Color.FromHex("#FFFFFF88");
        var colors = EntityQueryEnumerator<GhostColorComponent, SpriteComponent>();
        while (colors.MoveNext(out var uid, out var color, out var sprite))
        {
            _sprite.SetColor((uid, sprite), color.Color ?? defaultColor);
        }
    }
}
