using static Robust.Shared.Utility.SpriteSpecifier;

namespace Content.Client._RMC14.Xenonids.Hud;

public sealed partial class XenoHudOverlay
{
    private static Rsi[] CreateBarIcons(string prefix, string zero = "0")
    {
        var icons = new Rsi[11];
        for (var level = 0; level < icons.Length; level++)
            icons[level] = new Rsi(RsiPath, prefix + (level == 0 ? zero : (level * 10).ToString()));

        return icons;
    }
}
