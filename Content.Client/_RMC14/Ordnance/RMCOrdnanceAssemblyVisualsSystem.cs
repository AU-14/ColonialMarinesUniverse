using Content.Shared._RMC14.Ordnance;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._RMC14.Ordnance;

public sealed class RMCOrdnanceAssemblyVisualsSystem : VisualizerSystem<RMCOrdnanceAssemblyComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, RMCOrdnanceAssemblyComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (AppearanceSystem.TryGetData(uid, RMCAssemblyVisualKey.LeftType, out RMCOrdnancePartType left, args.Component))
            SpriteSystem.LayerSetSprite((uid, args.Sprite), 0, MakeSpecifier(left, true));

        if (AppearanceSystem.TryGetData(uid, RMCAssemblyVisualKey.RightType, out RMCOrdnancePartType right, args.Component))
            SpriteSystem.LayerSetSprite((uid, args.Sprite), 1, MakeSpecifier(right, false));
    }

    private static SpriteSpecifier.Rsi MakeSpecifier(RMCOrdnancePartType type, bool left)
    {
        var rsi = type switch
        {
            RMCOrdnancePartType.RMCOrdnanceIgniter => "igniter",
            RMCOrdnancePartType.RMCOrdnanceTimer => "timer",
            RMCOrdnancePartType.RMCOrdnanceSignaler => "signaller",
            RMCOrdnancePartType.RMCOrdnanceProximitySensor => "prox",
            _ => "igniter"
        };

        var state = type switch
        {
            RMCOrdnancePartType.RMCOrdnanceIgniter => left ? "igniter_left" : "igniter_right",
            RMCOrdnancePartType.RMCOrdnanceTimer => left ? "timer_left" : "timer_right",
            RMCOrdnancePartType.RMCOrdnanceSignaler => left ? "signaller_left" : "signaller_right",
            RMCOrdnancePartType.RMCOrdnanceProximitySensor => left ? "prox_left" : "prox_right",
            _ => "igniter_left"
        };

        return new SpriteSpecifier.Rsi(new ResPath($"_RMC14/Ordnance/{rsi}.rsi"), state);
    }
}
