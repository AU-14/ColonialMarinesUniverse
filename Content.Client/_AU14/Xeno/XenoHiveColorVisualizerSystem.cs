/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.

using Content.Shared._RMC14.Xenonids.Hive;
using Robust.Client.GameObjects;

namespace Content.Client._AU14.Xeno;

public sealed partial class XenoHiveColorVisualizerSystem : VisualizerSystem<HiveMemberComponent>
{
    [Dependency] private SpriteSystem _sprite = default!;
    protected override void OnAppearanceChange(EntityUid uid, HiveMemberComponent component, ref AppearanceChangeEvent args)
    {
        base.OnAppearanceChange(uid, component, ref args);
        if (TryComp<HiveComponent>(component.Hive, out var hive))
        {
            Color col = hive.HiveColor;
            _sprite.SetColor(uid, col);
        }
    }
}
